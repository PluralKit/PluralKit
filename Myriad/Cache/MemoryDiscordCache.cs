using System.Collections.Concurrent;

using Myriad.Types;

namespace Myriad.Cache;

public class MemoryDiscordCache: IDiscordCache
{
    private readonly ConcurrentDictionary<ulong, Channel> _channels = new();
    private readonly ConcurrentDictionary<ulong, GuildMemberPartial> _guildMembers = new();
    private readonly ConcurrentDictionary<ulong, CachedGuild> _guilds = new();
    private readonly ConcurrentDictionary<ulong, Role> _roles = new();
    private readonly ConcurrentDictionary<ulong, User> _users = new();
    private ulong? _ownUserId { get; set; }

    public ValueTask SaveGuild(Guild guild)
    {
        if (!_guilds.ContainsKey(guild.Id))
        {
            _guilds[guild.Id] = new CachedGuild(guild);
        }
        else
        {
            var channels = _guilds[guild.Id].Channels;
            _guilds[guild.Id] = new CachedGuild(guild)
            {
                Channels = channels,
            };
        }

        foreach (var role in guild.Roles)
            // Don't call SaveRole because that updates guild state
            // and we just got a brand new one :)
            _roles[role.Id] = role;

        return default;
    }

    public async ValueTask SaveChannel(Channel channel)
    {
        _channels[channel.Id] = channel;

        if (channel.GuildId != null && _guilds.TryGetValue(channel.GuildId.Value, out var guild))
            guild.Channels.TryAdd(channel.Id, true);

        if (channel.Recipients != null)
            foreach (var recipient in channel.Recipients)
                await SaveUser(recipient);
    }

    public ValueTask SaveOwnUser(ulong userId)
    {
        // this (hopefully) never changes at runtime, so we skip out on re-assigning it
        if (_ownUserId == null)
            _ownUserId = userId;

        return default;
    }

    public ValueTask SaveUser(User user)
    {
        _users[user.Id] = user;
        return default;
    }

    public ValueTask SaveSelfMember(ulong guildId, GuildMemberPartial member)
    {
        _guildMembers[guildId] = member;
        return default;
    }

    public ValueTask SaveRole(ulong guildId, Role role)
    {
        _roles[role.Id] = role;

        if (_guilds.TryGetValue(guildId, out var guild))
        {
            // TODO: this code is stinky
            var found = false;
            for (var i = 0; i < guild.Guild.Roles.Length; i++)
            {
                if (guild.Guild.Roles[i].Id != role.Id)
                    continue;

                guild.Guild.Roles[i] = role;
                found = true;
            }

            if (!found)
                _guilds[guildId] = guild with
                {
                    Guild = guild.Guild with { Roles = guild.Guild.Roles.Concat(new[] { role }).ToArray() }
                };
        }

        return default;
    }

    public ValueTask SaveDmChannelStub(ulong channelId)
    {
        // Use existing channel object if present, otherwise add a stub
        // We may get a message create before channel create and we want to have it saved
        _channels.GetOrAdd(channelId, id => new Channel { Id = id, Type = Channel.ChannelType.Dm });
        return default;
    }

    public ValueTask RemoveGuild(ulong guildId)
    {
        _guilds.TryRemove(guildId, out _);
        return default;
    }

    public ValueTask RemoveChannel(ulong channelId)
    {
        if (!_channels.TryRemove(channelId, out var channel))
            return default;

        if (channel.GuildId != null && _guilds.TryGetValue(channel.GuildId.Value, out var guild))
            guild.Channels.TryRemove(channel.Id, out _);

        return default;
    }

    public ValueTask RemoveUser(ulong userId)
    {
        _users.TryRemove(userId, out _);
        return default;
    }

    public Task<ulong> GetOwnUser() => Task.FromResult(_ownUserId!.Value);

    public ValueTask RemoveRole(ulong guildId, ulong roleId)
    {
        _roles.TryRemove(roleId, out _);
        return default;
    }

    public Task<Guild?> TryGetGuild(ulong guildId)
    {
        _guilds.TryGetValue(guildId, out var cg);
        return Task.FromResult(cg?.Guild);
    }

    public Task<Channel?> TryGetChannel(ulong channelId)
    {
        _channels.TryGetValue(channelId, out var channel);
        return Task.FromResult(channel);
    }

    public Task<User?> TryGetUser(ulong userId)
    {
        _users.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    public Task<GuildMemberPartial?> TryGetSelfMember(ulong guildId)
    {
        _guildMembers.TryGetValue(guildId, out var guildMember);
        return Task.FromResult(guildMember);
    }

    public Task<Role?> TryGetRole(ulong roleId)
    {
        _roles.TryGetValue(roleId, out var role);
        return Task.FromResult(role);
    }

    public IAsyncEnumerable<Guild> GetAllGuilds()
    {
        return _guilds.Values
            .Select(g => g.Guild)
            .ToAsyncEnumerable();
    }

    public Task<IEnumerable<Channel>> GetGuildChannels(ulong guildId)
    {
        if (!_guilds.TryGetValue(guildId, out var guild))
            throw new ArgumentException("Guild not found", nameof(guildId));

        return Task.FromResult(guild.Channels.Keys.Select(c => _channels[c]));
    }

    private record CachedGuild(Guild Guild)
    {
        public ConcurrentDictionary<ulong, bool> Channels { get; init; } = new();
    }
}