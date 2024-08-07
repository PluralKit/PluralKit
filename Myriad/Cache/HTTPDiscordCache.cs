using Serilog;
using System.Net;
using System.Net.Http.Json;

using Myriad.Types;

namespace Myriad.Cache;

#pragma warning disable 4014
public class HttpDiscordCache: IDiscordCache
{
    private readonly ILogger _logger;
    private readonly HttpClient _client;
    private readonly string _cacheEndpoint;
    private readonly ulong _ownUserId;

    public HttpDiscordCache(ILogger logger, HttpClient client, string cacheEndpoint, ulong ownUserId)
    {
        _logger = logger;
        _client = client;
        _cacheEndpoint = cacheEndpoint;
        _ownUserId = ownUserId;
    }

    public ValueTask SaveGuild(Guild guild) => default;
    public ValueTask SaveChannel(Channel channel) => default;
    public ValueTask SaveUser(User user) => default;
    public ValueTask SaveSelfMember(ulong guildId, GuildMemberPartial member) => default;
    public ValueTask SaveRole(ulong guildId, Myriad.Types.Role role) => default;

    public ValueTask SaveDmChannelStub(ulong channelId) => default;
    //    {
    // Use existing channel object if present, otherwise add a stub
    // We may get a message create before channel create and we want to have it saved

    // TODO

    //        if (await TryGetChannel(channelId) == null)
    //            await db.HashSetAsync("channels", channelId.HashWrapper(new CachedChannel()
    //            {
    //                Id = channelId,
    //                Type = (int)Channel.ChannelType.Dm,
    //            }));
    //    }

    public ValueTask RemoveGuild(ulong guildId) => default;
    public ValueTask RemoveChannel(ulong channelId) => default;
    public ValueTask RemoveUser(ulong userId) => default;
    public ValueTask RemoveRole(ulong guildId, ulong roleId) => default;

    public ulong GetOwnUser() => _ownUserId;

    // todo: cluster
    private async Task<T?> QueryCache<T>(string endpoint)
    {
        var response = await _client.GetAsync($"{_cacheEndpoint}{endpoint}");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;

        if (response.StatusCode != HttpStatusCode.Found)
            throw new Exception("failed to query http cache");

        return await response.Content.ReadFromJsonAsync<T>();
    }

    public Task<Guild?> TryGetGuild(ulong guildId)
        => QueryCache<Guild?>($"/guilds/{guildId}");

    public Task<Channel?> TryGetChannel(ulong channelId)
        => QueryCache<Channel?>($"/channels/{channelId}");

    // this should be a GetUserCached method on nirn-proxy (it's always called as GetOrFetchUser)
    // so just return nothing
    public Task<User?> TryGetUser(ulong userId)
        => Task.FromResult<User?>(null);

    public Task<GuildMemberPartial?> TryGetSelfMember(ulong guildId)
        => QueryCache<GuildMemberPartial?>($"/guilds/{guildId}/self_member");

    public Task<Myriad.Types.Role?> TryGetRole(ulong roleId)
        => QueryCache<Role?>($"/roles/{roleId}");

    public IAsyncEnumerable<Guild> GetAllGuilds()
    {
        // return _guilds.Values
        //     .Select(g => g.Guild)
        //     .ToAsyncEnumerable();
        return new Guild[] { }.ToAsyncEnumerable();
    }

    public Task<IEnumerable<Channel>> GetGuildChannels(ulong guildId)
        => QueryCache<IEnumerable<Channel>>($"/guilds/{guildId}/channels");
}