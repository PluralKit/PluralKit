using System.Collections.Generic;
using System.Threading.Tasks;

using Myriad.Types;

namespace Myriad.Cache
{
    public interface IDiscordCache
    {
        public ValueTask SaveGuild(Guild guild);
        public ValueTask SaveChannel(Channel channel);
        public ValueTask SaveUser(User user);
        public ValueTask SaveRole(ulong guildId, Role role);
        public ValueTask SaveDmChannelStub(ulong channelId);

        public ValueTask RemoveGuild(ulong guildId);
        public ValueTask RemoveChannel(ulong channelId);
        public ValueTask RemoveUser(ulong userId);
        public ValueTask RemoveRole(ulong guildId, ulong roleId);

        public Task<bool> TryGetGuild(ulong guildId, out Guild guild);
        public Task<bool> TryGetChannel(ulong channelId, out Channel channel);
        public Task<bool> TryGetDmChannel(ulong userId, out Channel channel);
        public Task<bool> TryGetUser(ulong userId, out User user);
        public Task<bool> TryGetRole(ulong roleId, out Role role);

        public IAsyncEnumerable<Guild> GetAllGuilds();
        public Task<IEnumerable<Channel>> GetGuildChannels(ulong guildId);
    }
}