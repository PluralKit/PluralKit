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

        public bool TryGetGuild(ulong guildId, out Guild guild);
        public bool TryGetChannel(ulong channelId, out Channel channel);
        public bool TryGetDmChannel(ulong userId, out Channel channel);
        public bool TryGetUser(ulong userId, out User user);
        public bool TryGetRole(ulong roleId, out Role role);

        public IAsyncEnumerable<Guild> GetAllGuilds();
        public IEnumerable<Channel> GetGuildChannels(ulong guildId);
    }
}