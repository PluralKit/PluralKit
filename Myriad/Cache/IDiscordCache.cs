using Myriad.Types;

namespace Myriad.Cache;

public interface IDiscordCache
{
    public ValueTask SaveGuild(Guild guild);
    public ValueTask SaveChannel(Channel channel);
    public ValueTask SaveUser(User user);
    public ValueTask SaveSelfMember(ulong guildId, GuildMemberPartial member);
    public ValueTask SaveRole(ulong guildId, Role role);
    public ValueTask SaveDmChannelStub(ulong channelId);

    public ValueTask RemoveGuild(ulong guildId);
    public ValueTask RemoveChannel(ulong channelId);
    public ValueTask RemoveUser(ulong userId);
    public ValueTask RemoveRole(ulong guildId, ulong roleId);

    internal ulong GetOwnUser();
    public Task<Guild?> TryGetGuild(ulong guildId);
    public Task<Channel?> TryGetChannel(ulong guildId, ulong channelId);
    public Task<User?> TryGetUser(ulong userId);
    public Task<GuildMemberPartial?> TryGetSelfMember(ulong guildId);

    public Task<IEnumerable<Channel>> GetGuildChannels(ulong guildId);
}