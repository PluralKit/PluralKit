using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Myriad.Types;

namespace Myriad.Cache
{
    public class MemoryDiscordCache: IDiscordCache
    {
        private readonly ConcurrentDictionary<ulong, Channel> _channels = new();
        private readonly ConcurrentDictionary<ulong, ulong> _dmChannels = new();
        private readonly ConcurrentDictionary<ulong, CachedGuild> _guilds = new();
        private readonly ConcurrentDictionary<ulong, Role> _roles = new();
        private readonly ConcurrentDictionary<ulong, User> _users = new();

        public ValueTask SaveGuild(Guild guild)
        {
            SaveGuildRaw(guild);

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
            {
                foreach (var recipient in channel.Recipients)
                {
                    _dmChannels[recipient.Id] = channel.Id;
                    await SaveUser(recipient);
                }
            }
        }

        public ValueTask SaveUser(User user)
        {
            _users[user.Id] = user;
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
                {
                    _guilds[guildId] = guild with
                    {
                        Guild = guild.Guild with
                        {
                            Roles = guild.Guild.Roles.Concat(new[] { role }).ToArray()
                        }
                    };
                }
            }

            return default;
        }

        public ValueTask SaveDmChannelStub(ulong channelId)
        {
            // Use existing channel object if present, otherwise add a stub
            // We may get a message create before channel create and we want to have it saved
            _channels.GetOrAdd(channelId, id => new Channel
            {
                Id = id,
                Type = Channel.ChannelType.Dm
            });
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

        public ValueTask RemoveRole(ulong guildId, ulong roleId)
        {
            _roles.TryRemove(roleId, out _);
            return default;
        }

        public bool TryGetGuild(ulong guildId, out Guild guild)
        {
            if (_guilds.TryGetValue(guildId, out var cg))
            {
                guild = cg.Guild;
                return true;
            }

            guild = null!;
            return false;
        }

        public bool TryGetChannel(ulong channelId, out Channel channel) =>
            _channels.TryGetValue(channelId, out channel!);

        public bool TryGetDmChannel(ulong userId, out Channel channel)
        {
            channel = default!;
            if (!_dmChannels.TryGetValue(userId, out var channelId))
                return false;
            return TryGetChannel(channelId, out channel);
        }

        public bool TryGetUser(ulong userId, out User user) =>
            _users.TryGetValue(userId, out user!);

        public bool TryGetRole(ulong roleId, out Role role) =>
            _roles.TryGetValue(roleId, out role!);

        public IAsyncEnumerable<Guild> GetAllGuilds()
        {
            return _guilds.Values
                .Select(g => g.Guild)
                .ToAsyncEnumerable();
        }

        public IEnumerable<Channel> GetGuildChannels(ulong guildId)
        {
            if (!_guilds.TryGetValue(guildId, out var guild))
                throw new ArgumentException("Guild not found", nameof(guildId));

            return guild.Channels.Keys.Select(c => _channels[c]);
        }

        private CachedGuild SaveGuildRaw(Guild guild) =>
            _guilds.GetOrAdd(guild.Id, (_, g) => new CachedGuild(g), guild);

        private record CachedGuild(Guild Guild)
        {
            public readonly ConcurrentDictionary<ulong, bool> Channels = new();
        }
    }
}