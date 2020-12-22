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

        public ValueTask RemoveGuild(ulong guildId);
        public ValueTask RemoveChannel(ulong channelId);
        public ValueTask RemoveUser(ulong userId);
        public ValueTask RemoveRole(ulong guildId, ulong roleId);

        public ValueTask<Guild?> GetGuild(ulong guildId);
        public ValueTask<Channel?> GetChannel(ulong channelId);
        public ValueTask<User?> GetUser(ulong userId);
        public ValueTask<Role?> GetRole(ulong roleId);

        public IAsyncEnumerable<Guild> GetAllGuilds();
        public ValueTask<IEnumerable<Channel>> GetGuildChannels(ulong guildId);
    }
}