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
        private readonly ConcurrentDictionary<ulong, Channel> _channels;
        private readonly ConcurrentDictionary<ulong, CachedGuild> _guilds;
        private readonly ConcurrentDictionary<ulong, Role> _roles;
        private readonly ConcurrentDictionary<ulong, User> _users;

        public MemoryDiscordCache()
        {
            _guilds = new ConcurrentDictionary<ulong, CachedGuild>();
            _channels = new ConcurrentDictionary<ulong, Channel>();
            _users = new ConcurrentDictionary<ulong, User>();
            _roles = new ConcurrentDictionary<ulong, Role>();
        }

        public ValueTask SaveGuild(Guild guild)
        {
            SaveGuildRaw(guild);

            foreach (var role in guild.Roles)
                // Don't call SaveRole because that updates guild state
                // and we just got a brand new one :)
                _roles[role.Id] = role;

            return default;
        }

        public ValueTask SaveChannel(Channel channel)
        {
            _channels[channel.Id] = channel;

            if (channel.GuildId != null && _guilds.TryGetValue(channel.GuildId.Value, out var guild))
                guild.Channels.TryAdd(channel.Id, true);

            return default;
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
                    _guilds[guildId] = guild with {
                        Guild = guild.Guild with {
                            Roles = guild.Guild.Roles.Concat(new[] { role}).ToArray()
                        }
                    };
                }
            }

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

        public ValueTask<Guild?> GetGuild(ulong guildId) => new(_guilds.GetValueOrDefault(guildId)?.Guild);

        public ValueTask<Channel?> GetChannel(ulong channelId) => new(_channels.GetValueOrDefault(channelId));

        public ValueTask<User?> GetUser(ulong userId) => new(_users.GetValueOrDefault(userId));

        public ValueTask<Role?> GetRole(ulong roleId) => new(_roles.GetValueOrDefault(roleId));

        public async IAsyncEnumerable<Guild> GetAllGuilds()
        {
            foreach (var guild in _guilds.Values)
                yield return guild.Guild;
        }

        public ValueTask<IEnumerable<Channel>> GetGuildChannels(ulong guildId)
        {
            if (!_guilds.TryGetValue(guildId, out var guild))
                throw new ArgumentException("Guild not found", nameof(guildId));

            return new ValueTask<IEnumerable<Channel>>(guild.Channels.Keys.Select(c => _channels[c]));
        }

        private CachedGuild SaveGuildRaw(Guild guild) =>
            _guilds.GetOrAdd(guild.Id, (_, g) => new CachedGuild(g), guild);

        private record CachedGuild(Guild Guild)
        {
            public readonly ConcurrentDictionary<ulong, bool> Channels = new();
        }
    }
}