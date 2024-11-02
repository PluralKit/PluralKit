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

    private readonly MemoryDiscordCache _innerCache;

    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public EventHandler<(bool?, string)> OnDebug;

    public HttpDiscordCache(ILogger logger, HttpClient client, string cacheEndpoint, int shardCount, ulong ownUserId, bool useInnerCache)
    {
        _logger = logger;
        _client = client;
        _cacheEndpoint = new Uri(cacheEndpoint);
        _shardCount = shardCount;
        _ownUserId = ownUserId;
        _jsonSerializerOptions = new JsonSerializerOptions().ConfigureForMyriad();
        if (useInnerCache) _innerCache = new MemoryDiscordCache(ownUserId);
    }

    public ValueTask SaveGuild(Guild guild) => _innerCache?.SaveGuild(guild) ?? default;
    public ValueTask SaveChannel(Channel channel) => _innerCache?.SaveChannel(channel) ?? default;
    public ValueTask SaveUser(User user) => default;
    public ValueTask SaveSelfMember(ulong guildId, GuildMemberPartial member) => _innerCache?.SaveSelfMember(guildId, member) ?? default;
    public ValueTask SaveRole(ulong guildId, Myriad.Types.Role role) => _innerCache?.SaveRole(guildId, role) ?? default;
    public ValueTask SaveDmChannelStub(ulong channelId) => _innerCache?.SaveDmChannelStub(channelId) ?? default;
    public ValueTask RemoveGuild(ulong guildId) => _innerCache?.RemoveGuild(guildId) ?? default;
    public ValueTask RemoveChannel(ulong channelId) => _innerCache?.RemoveChannel(channelId) ?? default;
    public ValueTask RemoveUser(ulong userId) => _innerCache?.RemoveUser(userId) ?? default;
    public ValueTask RemoveRole(ulong guildId, ulong roleId) => _innerCache?.RemoveRole(guildId, roleId) ?? default;

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

    public async Task<Guild?> TryGetGuild(ulong guildId)
    {
        var hres = await QueryCache<Guild?>($"/guilds/{guildId}", guildId);
        if (_innerCache == null) return hres;
        var lres = await _innerCache.TryGetGuild(guildId);

        if (lres == null && hres == null) return null;
        if (lres == null)
        {
            _logger.Warning($"TryGetGuild({guildId}) was only successful on remote cache");
            OnDebug(null, (true, "guild"));
            return hres;
        }
        if (hres == null)
        {
            _logger.Warning($"TryGetGuild({guildId}) was only successful on local cache");
            OnDebug(null, (false, "guild"));
            return lres;
        }
        return hres;
    }

    public async Task<Channel?> TryGetChannel(ulong guildId, ulong channelId)
    {
        var hres = await QueryCache<Channel?>($"/guilds/{guildId}/channels/{channelId}", guildId);
        if (_innerCache == null) return hres;
        var lres = await _innerCache.TryGetChannel(guildId, channelId);
        if (lres == null && hres == null) return null;
        if (lres == null)
        {
            _logger.Warning($"TryGetChannel({guildId}, {channelId}) was only successful on remote cache");
            OnDebug(null, (true, "channel"));
            return hres;
        }
        if (hres == null)
        {
            _logger.Warning($"TryGetChannel({guildId}, {channelId}) was only successful on local cache");
            OnDebug(null, (false, "channel"));
            return lres;
        }
        return hres;
    }

    // this should be a GetUserCached method on nirn-proxy (it's always called as GetOrFetchUser)
    // so just return nothing
    public Task<User?> TryGetUser(ulong userId)
        => Task.FromResult<User?>(null);

    public async Task<GuildMemberPartial?> TryGetSelfMember(ulong guildId)
    {
        var hres = await QueryCache<GuildMemberPartial?>($"/guilds/{guildId}/members/@me", guildId);
        if (_innerCache == null) return hres;
        var lres = await _innerCache.TryGetSelfMember(guildId);
        if (lres == null && hres == null) return null;
        if (lres == null)
        {
            _logger.Warning($"TryGetSelfMember({guildId}) was only successful on remote cache");
            OnDebug(null, (true, "self_member"));
            return hres;
        }
        if (hres == null)
        {
            _logger.Warning($"TryGetSelfMember({guildId}) was only successful on local cache");
            OnDebug(null, (false, "self_member"));
            return lres;
        }
        return hres;
    }

    //    public async Task<PermissionSet> BotChannelPermissions(ulong guildId, ulong channelId)
    //    {
    //        // todo: local cache throws rather than returning null
    //        // we need to throw too, and try/catch local cache here
    //        var lres = await _innerCache.BotPermissionsIn(guildId, channelId);
    //        var hres = await QueryCache<PermissionSet?>($"/guilds/{guildId}/channels/{channelId}/permissions/@me", guildId);
    //        if (lres == null && hres == null) return null;
    //        if (lres == null)
    //        {
    //            _logger.Warning($"TryGetChannel({guildId}, {channelId}) was only successful on remote cache");
    //           OnDebug(null, (true, "botchannelperms"));
    //            return hres;
    //        }
    //        if (hres == null)
    //        {
    //            _logger.Warning($"TryGetChannel({guildId}, {channelId}) was only successful on local cache");
    //            OnDebug(null, (false, "botchannelperms"));
    //            return lres;
    //        }
    //
    //        // this one is easy to check, so let's check it
    //        if ((int)lres != (int)hres)
    //        {
    //            // trust local
    //            _logger.Warning($"got different permissions for {channelId} (local {(int)lres}, remote {(int)hres})");
    //            OnDebug(null, (null, "botchannelperms"));
    //            return lres;
    //        }
    //        return hres;
    //    }

    public async Task<IEnumerable<Channel>> GetGuildChannels(ulong guildId)
    {
        var hres = await QueryCache<IEnumerable<Channel>>($"/guilds/{guildId}/channels", guildId);
        if (_innerCache == null) return hres;
        var lres = await _innerCache.GetGuildChannels(guildId);
        if (lres == null && hres == null) return null;
        if (lres == null)
        {
            _logger.Warning($"GetGuildChannels({guildId}) was only successful on remote cache");
            OnDebug(null, (true, "guild_channels"));
            return hres;
        }
        if (hres == null)
        {
            _logger.Warning($"GetGuildChannels({guildId}) was only successful on local cache");
            OnDebug(null, (false, "guild_channels"));
            return lres;
        }
        return hres;
    }
}