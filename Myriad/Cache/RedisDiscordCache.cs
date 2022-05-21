using Google.Protobuf;

using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

using Serilog;

using Myriad.Types;

namespace Myriad.Cache;

#pragma warning disable 4014
public class RedisDiscordCache: IDiscordCache
{
    private readonly ILogger _logger;
    public RedisDiscordCache(ILogger logger)
    {
        _logger = logger;
    }

    private ConnectionMultiplexer _redis { get; set; }
    private ulong _ownUserId { get; set; }

    public async Task InitAsync(string addr, ulong ownUserId)
    {
        _redis = await ConnectionMultiplexer.ConnectAsync(addr);
        _ownUserId = ownUserId;
    }

    private IDatabase db => _redis.GetDatabase().WithKeyPrefix("discord:");

    public async ValueTask SaveGuild(Guild guild)
    {
        _logger.Verbose("Saving guild {GuildId} to redis", guild.Id);

        var g = new CachedGuild();
        g.Id = guild.Id;
        g.Name = guild.Name;
        g.OwnerId = guild.OwnerId;
        g.PremiumTier = (int)guild.PremiumTier;

        var tr = db.CreateTransaction();

        tr.HashSetAsync("guilds", guild.Id.HashWrapper(g));

        foreach (var role in guild.Roles)
        {
            // Don't call SaveRole because that updates guild state
            // and we just got a brand new one :)
            // actually with redis it doesn't update guild state, but we're still doing it here because transaction
            tr.HashSetAsync("roles", role.Id.HashWrapper(new CachedRole()
            {
                Id = role.Id,
                Name = role.Name,
                Position = role.Position,
                Permissions = (ulong)role.Permissions,
                Mentionable = role.Mentionable,
            }));

            tr.HashSetAsync($"guild_roles:{guild.Id}", role.Id, true, When.NotExists);
        }

        await tr.ExecuteAsync();
    }

    public async ValueTask SaveChannel(Channel channel)
    {
        _logger.Verbose("Saving channel {ChannelId} to redis", channel.Id);

        await db.HashSetAsync("channels", channel.Id.HashWrapper(channel.ToProtobuf()));

        if (channel.GuildId != null)
            await db.HashSetAsync($"guild_channels:{channel.GuildId.Value}", channel.Id, true, When.NotExists);

        // todo: use a transaction for this?
        if (channel.Recipients != null)
            foreach (var recipient in channel.Recipients)
                await SaveUser(recipient);
    }

    public ValueTask SaveOwnUser(ulong userId)
    {
        // we get the own user ID in InitAsync, so no need to save it here
        return default;
    }

    public async ValueTask SaveUser(User user)
    {
        _logger.Verbose("Saving user {UserId} to redis", user.Id);

        var u = new CachedUser()
        {
            Id = user.Id,
            Username = user.Username,
            Discriminator = user.Discriminator,
            Bot = user.Bot,
        };

        if (user.Avatar != null)
            u.Avatar = user.Avatar;

        await db.HashSetAsync("users", user.Id.HashWrapper(u));
    }

    public async ValueTask SaveSelfMember(ulong guildId, GuildMemberPartial member)
    {
        _logger.Verbose("Saving self member for guild {GuildId} to redis", guildId);

        var gm = new CachedGuildMember();
        foreach (var role in member.Roles)
            gm.Roles.Add(role);

        await db.HashSetAsync("members", guildId.HashWrapper(gm));
    }

    public async ValueTask SaveRole(ulong guildId, Myriad.Types.Role role)
    {
        _logger.Verbose("Saving role {RoleId} in {GuildId} to redis", role.Id, guildId);

        await db.HashSetAsync("roles", role.Id.HashWrapper(new CachedRole()
        {
            Id = role.Id,
            Mentionable = role.Mentionable,
            Name = role.Name,
            Permissions = (ulong)role.Permissions,
            Position = role.Position,
        }));

        await db.HashSetAsync($"guild_roles:{guildId}", role.Id, true, When.NotExists);
    }

    public async ValueTask SaveDmChannelStub(ulong channelId)
    {
        // Use existing channel object if present, otherwise add a stub
        // We may get a message create before channel create and we want to have it saved

        if (await TryGetChannel(channelId) == null)
            await db.HashSetAsync("channels", channelId.HashWrapper(new CachedChannel()
            {
                Id = channelId,
                Type = (int)Channel.ChannelType.Dm,
            }));
    }

    public async ValueTask RemoveGuild(ulong guildId)
        => await db.HashDeleteAsync("guilds", guildId);

    public async ValueTask RemoveChannel(ulong channelId)
    {
        var oldChannel = await TryGetChannel(channelId);

        if (oldChannel == null)
            return;

        await db.HashDeleteAsync("channels", channelId);

        if (oldChannel.GuildId != null)
            await db.HashDeleteAsync($"guild_channels:{oldChannel.GuildId.Value}", oldChannel.Id);
    }

    public async ValueTask RemoveUser(ulong userId)
        => await db.HashDeleteAsync("users", userId);

    // todo: try getting this from redis if we don't have it yet
    public Task<ulong> GetOwnUser() => Task.FromResult(_ownUserId);

    public async ValueTask RemoveRole(ulong guildId, ulong roleId)
    {
        await db.HashDeleteAsync("roles", roleId);
        await db.HashDeleteAsync($"guild_roles:{guildId}", roleId);
    }

    public async Task<Guild?> TryGetGuild(ulong guildId)
    {
        var redisGuild = await db.HashGetAsync("guilds", guildId);
        if (redisGuild.IsNullOrEmpty)
            return null;

        var guild = ((byte[])redisGuild).Unmarshal<CachedGuild>();

        var redisRoles = await db.HashGetAllAsync($"guild_roles:{guildId}");

        // todo: put this in a transaction or something
        var roles = await Task.WhenAll(redisRoles.Select(r => TryGetRole((ulong)r.Name)));

#pragma warning disable 8619
        return guild.FromProtobuf() with { Roles = roles };
#pragma warning restore 8619
    }

    public async Task<Channel?> TryGetChannel(ulong channelId)
    {
        var redisChannel = await db.HashGetAsync("channels", channelId);
        if (redisChannel.IsNullOrEmpty)
            return null;

        return ((byte[])redisChannel).Unmarshal<CachedChannel>().FromProtobuf();
    }

    public async Task<User?> TryGetUser(ulong userId)
    {
        var redisUser = await db.HashGetAsync("users", userId);
        if (redisUser.IsNullOrEmpty)
            return null;

        return ((byte[])redisUser).Unmarshal<CachedUser>().FromProtobuf();
    }

    public async Task<GuildMemberPartial?> TryGetSelfMember(ulong guildId)
    {
        var redisMember = await db.HashGetAsync("members", guildId);
        if (redisMember.IsNullOrEmpty)
            return null;

        return new GuildMemberPartial()
        {
            Roles = ((byte[])redisMember).Unmarshal<CachedGuildMember>().Roles.ToArray()
        };
    }

    public async Task<Myriad.Types.Role?> TryGetRole(ulong roleId)
    {
        var redisRole = await db.HashGetAsync("roles", roleId);
        if (redisRole.IsNullOrEmpty)
            return null;

        var role = ((byte[])redisRole).Unmarshal<CachedRole>();

        return new Myriad.Types.Role()
        {
            Id = role.Id,
            Name = role.Name,
            Position = role.Position,
            Permissions = (PermissionSet)role.Permissions,
            Mentionable = role.Mentionable,
        };
    }

    public IAsyncEnumerable<Guild> GetAllGuilds()
    {
        // return _guilds.Values
        //     .Select(g => g.Guild)
        //     .ToAsyncEnumerable();
        return new Guild[] { }.ToAsyncEnumerable();
    }

    public async Task<IEnumerable<Channel>> GetGuildChannels(ulong guildId)
    {
        var redisChannels = await db.HashGetAllAsync($"guild_channels:{guildId}");
        if (redisChannels.Length == 0)
            throw new ArgumentException("Guild not found", nameof(guildId));

#pragma warning disable 8619
        return await Task.WhenAll(redisChannels.Select(c => TryGetChannel((ulong)c.Name)));
#pragma warning restore 8619
    }
}

internal static class CacheProtoExt
{
    public static Guild FromProtobuf(this CachedGuild guild)
        => new Guild()
        {
            Id = guild.Id,
            Name = guild.Name,
            OwnerId = guild.OwnerId,
            PremiumTier = (PremiumTier)guild.PremiumTier,
        };

    public static CachedChannel ToProtobuf(this Channel channel)
    {
        var c = new CachedChannel();
        c.Id = channel.Id;
        c.Type = (int)channel.Type;
        if (channel.Position != null)
            c.Position = channel.Position.Value;
        c.Name = channel.Name;
        if (channel.PermissionOverwrites != null)
            foreach (var overwrite in channel.PermissionOverwrites)
                c.PermissionOverwrites.Add(new Overwrite()
                {
                    Id = overwrite.Id,
                    Type = (int)overwrite.Type,
                    Allow = (ulong)overwrite.Allow,
                    Deny = (ulong)overwrite.Deny,
                });
        if (channel.GuildId != null)
            c.GuildId = channel.GuildId.Value;

        return c;
    }

    public static Channel FromProtobuf(this CachedChannel channel)
        => new Channel()
        {
            Id = channel.Id,
            Type = (Channel.ChannelType)channel.Type,
            Position = channel.Position,
            Name = channel.Name,
            PermissionOverwrites = channel.PermissionOverwrites
                .Select(x => new Channel.Overwrite()
                {
                    Id = x.Id,
                    Type = (Channel.OverwriteType)x.Type,
                    Allow = (PermissionSet)x.Allow,
                    Deny = (PermissionSet)x.Deny,
                }).ToArray(),
            GuildId = channel.HasGuildId ? channel.GuildId : null,
            ParentId = channel.HasParentId ? channel.ParentId : null,
        };

    public static User FromProtobuf(this CachedUser user)
        => new User()
        {
            Id = user.Id,
            Username = user.Username,
            Discriminator = user.Discriminator,
            Avatar = user.HasAvatar ? user.Avatar : null,
            Bot = user.Bot,
        };
}

internal static class RedisExt
{
    // convenience method
    public static HashEntry[] HashWrapper<T>(this ulong key, T value) where T : IMessage
        => new[] { new HashEntry(key, value.ToByteArray()) };
}

public static class ProtobufExt
{
    private static Dictionary<string, MessageParser> _parser = new();

    public static byte[] Marshal(this IMessage message) => message.ToByteArray();

    public static T Unmarshal<T>(this byte[] message) where T : IMessage<T>, new()
    {
        var type = typeof(T).ToString();
        if (_parser.ContainsKey(type))
            return (T)_parser[type].ParseFrom(message);
        else
        {
            _parser.Add(type, new MessageParser<T>(() => new T()));
            return Unmarshal<T>(message);
        }
    }
}