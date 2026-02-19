using PluralKit.Core;

using Serilog;

namespace PluralKit.Matrix;

public class VirtualUserService
{
    private readonly MatrixApiClient _api;
    private readonly MatrixConfig _config;
    private readonly MatrixRepository _repo;
    private readonly VirtualUserCacheService _cache;
    private readonly ILogger _logger;

    public VirtualUserService(MatrixApiClient api, MatrixConfig config, MatrixRepository repo,
        VirtualUserCacheService cache, ILogger logger)
    {
        _api = api;
        _config = config;
        _repo = repo;
        _cache = cache;
        _logger = logger.ForContext<VirtualUserService>();
    }

    /// <summary>
    /// Ensure the virtual user for this member is registered with the homeserver,
    /// has the correct display name and avatar, and is tracked in the database.
    /// </summary>
    public async Task EnsureRegistered(ProxyMember member, string memberHid, MessageContext ctx)
    {
        var localpart = $"_pk_{memberHid}";
        var mxid = $"@{localpart}:{_config.ServerName}";

        var existing = await _repo.GetVirtualUser(member.Id);
        if (existing == null)
        {
            // Register the virtual user with the homeserver
            await _api.RegisterUser(localpart);

            // Set display name
            var displayName = member.ProxyName(ctx);
            await _api.SetDisplayName(mxid, displayName);

            // Upload avatar if available
            string? avatarMxc = null;
            var avatarUrl = member.ProxyAvatar(ctx);
            if (avatarUrl != null)
            {
                try
                {
                    var (data, contentType) = await _api.DownloadMedia(avatarUrl);
                    avatarMxc = await _api.UploadMedia(data, contentType, "avatar.png");
                    await _api.SetAvatarUrl(mxid, avatarMxc);
                }
                catch (HttpRequestException ex)
                {
                    _logger.Error(ex, "Failed to upload avatar for member {MemberId} from {Url}", member.Id, avatarUrl);
                }
                catch (ArgumentException ex)
                {
                    _logger.Warning(ex, "Invalid avatar URL for member {MemberId}: {Url}", member.Id, avatarUrl);
                }
            }

            // Store in database
            await _repo.UpsertVirtualUser(member.Id, mxid, displayName, avatarMxc);
            _cache.MarkRegistered(mxid);
            return;
        }

        // Virtual user exists — check if profile needs updating
        var currentDisplayName = member.ProxyName(ctx);
        var needsSync = existing.DisplayName != currentDisplayName
            || existing.LastSynced == null
            || (DateTimeOffset.UtcNow - existing.LastSynced.Value).TotalHours > 1;

        if (needsSync)
        {
            await _api.SetDisplayName(mxid, currentDisplayName);

            // Re-upload avatar if needed
            var currentAvatar = member.ProxyAvatar(ctx);
            if (currentAvatar != null && existing.AvatarMxc == null)
            {
                try
                {
                    var (data, contentType) = await _api.DownloadMedia(currentAvatar);
                    var avatarMxc = await _api.UploadMedia(data, contentType, "avatar.png");
                    await _api.SetAvatarUrl(mxid, avatarMxc);
                    await _repo.UpdateVirtualUserAvatar(member.Id, avatarMxc);
                }
                catch (HttpRequestException ex)
                {
                    _logger.Error(ex, "Failed to update avatar for member {MemberId} from {Url}", member.Id, currentAvatar);
                }
                catch (ArgumentException ex)
                {
                    _logger.Warning(ex, "Invalid avatar URL for member {MemberId}: {Url}", member.Id, currentAvatar);
                }
            }

            await _repo.UpdateVirtualUserSync(member.Id, currentDisplayName);
        }
    }

    /// <summary>
    /// Ensure the virtual user has joined the specified room.
    /// Uses a cache to avoid redundant join requests.
    /// Returns false if the join was rejected.
    /// </summary>
    public async Task<bool> EnsureJoined(string virtualMxid, string roomId)
    {
        // Check in-memory cache first
        if (_cache.IsJoined(virtualMxid, roomId))
            return true;

        // Check database
        if (await _repo.CheckRoomJoined(virtualMxid, roomId))
        {
            _cache.MarkJoined(virtualMxid, roomId);
            return true;
        }

        // Join the room via the Matrix API
        if (!await _api.JoinRoom(roomId, virtualMxid))
            return false;

        // Store and cache the join
        await _repo.StoreRoomJoin(virtualMxid, roomId);
        _cache.MarkJoined(virtualMxid, roomId);

        _logger.Debug("Virtual user {Mxid} joined room {RoomId}", virtualMxid, roomId);
        return true;
    }
}
