using Serilog;
using System.Net;
using System.Text.Json;

using Myriad.Serialization;
using Myriad.Types;

namespace Myriad.Cache;

public class HttpDiscordCache: IDiscordCache
{
    private readonly ILogger _logger;
    private readonly HttpClient _client;
    private readonly Uri _cacheEndpoint;
    private readonly int _shardCount;
    private readonly ulong _ownUserId;

    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public HttpDiscordCache(ILogger logger, HttpClient client, string cacheEndpoint, int shardCount, ulong ownUserId)
    {
        _logger = logger;
        _client = client;
        _cacheEndpoint = new Uri(cacheEndpoint);
        _shardCount = shardCount;
        _ownUserId = ownUserId;
        _jsonSerializerOptions = new JsonSerializerOptions().ConfigureForMyriad();
    }

    public ValueTask SaveGuild(Guild guild) => default;
    public ValueTask SaveChannel(Channel channel) => default;
    public ValueTask SaveUser(User user) => default;
    public ValueTask SaveSelfMember(ulong guildId, GuildMemberPartial member) => default;
    public ValueTask SaveRole(ulong guildId, Myriad.Types.Role role) => default;
    public ValueTask SaveDmChannelStub(ulong channelId) => default;
    public ValueTask RemoveGuild(ulong guildId) => default;
    public ValueTask RemoveChannel(ulong channelId) => default;
    public ValueTask RemoveUser(ulong userId) => default;
    public ValueTask RemoveRole(ulong guildId, ulong roleId) => default;

    public ulong GetOwnUser() => _ownUserId;

    private async Task<T?> QueryCache<T>(string endpoint, ulong guildId)
    {
        var cluster = _cacheEndpoint.Authority;
        if (cluster.Contains(".service.consul"))
            // int(((guild_id >> 22) % shard_count) / 16)
            cluster = $"cluster{(int)(((guildId >> 22) % (ulong)_shardCount) / 16)}.{cluster}";

        var response = await _client.GetAsync($"{_cacheEndpoint.Scheme}://{cluster}{endpoint}");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;

        if (response.StatusCode != HttpStatusCode.Found)
            throw new Exception($"failed to query http cache: {response.StatusCode}");

        var plaintext = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(plaintext, _jsonSerializerOptions);
    }

    public Task<Guild?> TryGetGuild(ulong guildId)
        => QueryCache<Guild?>($"/guilds/{guildId}", guildId);

    public Task<Channel?> TryGetChannel(ulong guildId, ulong channelId)
        => QueryCache<Channel?>($"/guilds/{guildId}/channels/{channelId}", guildId);

    // this should be a GetUserCached method on nirn-proxy (it's always called as GetOrFetchUser)
    // so just return nothing
    public Task<User?> TryGetUser(ulong userId)
        => Task.FromResult<User?>(null);

    public Task<GuildMemberPartial?> TryGetSelfMember(ulong guildId)
        => QueryCache<GuildMemberPartial?>($"/guilds/{guildId}/members/@me", guildId);

    public Task<PermissionSet> BotChannelPermissions(ulong guildId, ulong channelId)
        => QueryCache<PermissionSet>($"/guilds/{guildId}/channels/{channelId}/permissions/@me", guildId);

    public Task<IEnumerable<Channel>> GetGuildChannels(ulong guildId)
        => QueryCache<IEnumerable<Channel>>($"/guilds/{guildId}/channels", guildId);
}