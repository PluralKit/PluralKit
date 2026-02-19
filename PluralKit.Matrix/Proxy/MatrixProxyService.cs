using NodaTime;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Matrix;

public class MatrixProxyService
{
    private readonly MatrixApiClient _api;
    private readonly MatrixRepository _repo;
    private readonly VirtualUserService _virtualUsers;
    private readonly ProxyMatcher _matcher;
    private readonly ModelRepository _coreRepo;
    private readonly MatrixLogService _logService;
    private readonly MatrixConfig _config;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public MatrixProxyService(MatrixApiClient api, MatrixRepository repo, VirtualUserService virtualUsers,
        ProxyMatcher matcher, ModelRepository coreRepo, MatrixLogService logService, MatrixConfig config,
        IClock clock, ILogger logger)
    {
        _api = api;
        _repo = repo;
        _virtualUsers = virtualUsers;
        _matcher = matcher;
        _coreRepo = coreRepo;
        _logService = logService;
        _config = config;
        _clock = clock;
        _logger = logger.ForContext<MatrixProxyService>();
    }

    public async Task<bool> HandleIncomingMessage(MatrixEvent evt)
    {
        var senderMxid = evt.Sender;
        var roomId = evt.RoomId;
        var messageContent = evt.Body ?? "";
        var prefix = _config.Prefix;

        var ctx = await _repo.GetMessageContext(senderMxid, roomId);

        if (ctx.SystemId == null)
            return false;

        if (ctx.InBlacklist)
            return false;

        var autoproxySettings = await _repo.GetAutoproxySettings(ctx.SystemId.Value, roomId);

        // Escape/unlatch only applies to text messages — media filenames could accidentally trigger these
        if (evt.MessageType == "m.text")
        {
            if (IsDisableAutoproxy(messageContent))
            {
                await _repo.UpdateAutoproxy(ctx.SystemId.Value, roomId, new AutoproxyPatch
                {
                    AutoproxyMode = AutoproxyMode.Off
                });
                return false;
            }

            if (autoproxySettings.AutoproxyMode == AutoproxyMode.Latch && IsUnlatch(messageContent))
            {
                await _repo.UpdateAutoproxy(ctx.SystemId.Value, roomId, new AutoproxyPatch
                {
                    AutoproxyMember = null
                });
                return false;
            }
        }

        var members = (await _repo.GetProxyMembers(senderMxid)).ToList();
        if (members.Count == 0) return false;

        ProxyMatch match;
        try
        {
            if (!_matcher.TryMatch(ctx, autoproxySettings, members, out match,
                    messageContent, prefix, false, ctx.AllowAutoproxy, ctx.CaseSensitiveProxyTags))
                return false;
        }
        catch (ProxyChecksFailedException ex)
        {
            if (ctx.ProxyErrorMessageEnabled)
            {
                _logger.Debug("Proxy check failed for {Sender}: {Message}", senderMxid, ex.Message);
                var botMxid = $"@{_config.BotLocalpart}:{_config.ServerName}";
                var txnId = $"pk_proxyerr_{evt.EventId}_{Guid.NewGuid():N}";
                try { await _api.SendMessage(roomId, botMxid, ex.Message, null, txnId); }
                catch (Exception sendEx) { _logger.Warning(sendEx, "Failed to send proxy error message"); }
            }
            return false;
        }

        await ExecuteProxy(evt, ctx, autoproxySettings, match);
        return true;
    }

    private async Task ExecuteProxy(MatrixEvent trigger, MessageContext ctx, AutoproxySettings autoproxySettings, ProxyMatch match)
    {
        var member = await _coreRepo.GetMember(match.Member.Id);
        var memberHid = member?.Hid ?? "unknown";
        var virtualMxid = $"@_pk_{memberHid}:{_config.ServerName}";
        var roomId = trigger.RoomId;

        await _virtualUsers.EnsureRegistered(match.Member, memberHid, ctx);
        if (!await _virtualUsers.EnsureJoined(virtualMxid, roomId))
        {
            _logger.Warning("Cannot proxy: virtual user {Mxid} failed to join {Room}", virtualMxid, roomId);
            return;
        }

        // Display name deduplication: if a different member has the same name, add invisible suffix
        var displayName = match.Member.ProxyName(ctx);
        var lastProxied = await _repo.GetLastProxiedInRoom(roomId);
        if (lastProxied != null
            && lastProxied.Value.Member != match.Member.Id
            && lastProxied.Value.DisplayName == displayName)
        {
            // Append hair space + Khmer vowel sign as invisible disambiguator (same as Discord logic)
            var suffixedName = displayName + "\u200a\u17b5";
            try { await _api.SetRoomDisplayName(roomId, virtualMxid, suffixedName); }
            catch (Exception ex) { _logger.Warning(ex, "Failed to set room display name suffix for {Mxid}", virtualMxid); }
        }

        var txnId = $"pk_{trigger.EventId}_{Guid.NewGuid():N}";

        string proxyEventId;
        if (trigger.IsMedia)
        {
            var mxcUrl = trigger.MediaUrl;
            if (mxcUrl == null)
            {
                _logger.Warning("Media message {EventId} missing mxc URL", trigger.EventId);
                return;
            }

            try
            {
                var (data, ct) = await _api.DownloadMxcMedia(mxcUrl);
                var newMxc = await _api.UploadMedia(data, ct, trigger.MediaFilename ?? "file");
                proxyEventId = await _api.SendMediaMessage(roomId, virtualMxid, trigger.MessageType!,
                    newMxc, trigger.MediaFilename ?? "file", trigger.MediaInfo, txnId);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to proxy media message {EventId} in {Room}", trigger.EventId, roomId);
                return;
            }
        }
        else
        {
            var content = match.ProxyContent ?? match.Content ?? "";
            proxyEventId = await _api.SendMessage(roomId, virtualMxid, content, null, txnId);
        }

        // Redact original — graceful failure if no permission or transient error
        try
        {
            var redactTxnId = $"pk_redact_{trigger.EventId}_{Guid.NewGuid():N}";
            await _api.RedactEvent(roomId, trigger.EventId, "Proxied by PluralKit", redactTxnId);
        }
        catch (HttpRequestException ex)
        {
            _logger.Warning(ex, "Failed to redact original message {EventId} in {Room}", trigger.EventId, roomId);
        }

        // Store record even if redaction failed — message was sent successfully
        try
        {
            await _repo.AddMessage(new MatrixMessage
            {
                ProxiedEventId = proxyEventId,
                OriginalEventId = trigger.EventId,
                RoomId = roomId,
                Member = match.Member.Id,
                SenderMxid = trigger.Sender,
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to store proxied message record for {EventId} in {Room}", proxyEventId, roomId);
        }

        // Update member stats (last message timestamp, message count)
        try { await _coreRepo.UpdateMemberForSentMessage(match.Member.Id); }
        catch (Exception ex) { _logger.Warning(ex, "Failed to update member stats for {MemberId}", match.Member.Id); }

        // Log proxy to configured log room
        try { await _logService.LogProxy(roomId, match.Member.ProxyName(ctx), proxyEventId); }
        catch (Exception ex) { _logger.Warning(ex, "Failed to log proxy event"); }

        if (autoproxySettings.AutoproxyMode == AutoproxyMode.Latch)
        {
            await _repo.UpdateAutoproxy(ctx.SystemId!.Value, roomId, new AutoproxyPatch
            {
                AutoproxyMember = match.Member.Id,
                LastLatchTimestamp = _clock.GetCurrentInstant(),
            });
        }

        _logger.Information("Proxied message from {Sender} as {VirtualUser} in {Room} (event: {EventId})",
            trigger.Sender, virtualMxid, roomId, proxyEventId);
    }

    public async Task HandleEdit(MatrixEvent evt)
    {
        var editedEventId = evt.EditedEventId;
        if (editedEventId == null) return;

        var proxiedMsg = await _repo.GetMessageByOriginal(editedEventId);
        if (proxiedMsg == null) return;

        if (proxiedMsg.SenderMxid != evt.Sender) return;

        if (proxiedMsg.Member == null)
        {
            _logger.Warning("Cannot proxy edit for {EventId}: member was deleted", editedEventId);
            return;
        }

        var member = await _coreRepo.GetMember(proxiedMsg.Member.Value);
        if (member == null)
        {
            _logger.Warning("Cannot proxy edit for {EventId}: member {MemberId} not found", editedEventId, proxiedMsg.Member.Value);
            return;
        }

        var virtualMxid = $"@_pk_{member.Hid}:{_config.ServerName}";

        if (!await _virtualUsers.EnsureJoined(virtualMxid, evt.RoomId))
        {
            _logger.Warning("Cannot proxy edit: virtual user {Mxid} failed to join {Room}", virtualMxid, evt.RoomId);
            return;
        }

        var newBody = (string?)evt.NewContent?["body"] ?? evt.Body ?? "";
        var newFormattedBody = (string?)evt.NewContent?["formatted_body"];

        var txnId = $"pk_edit_{evt.EventId}_{Guid.NewGuid():N}";
        await _api.SendEdit(evt.RoomId, virtualMxid, proxiedMsg.ProxiedEventId, newBody, newFormattedBody, txnId);

        _logger.Information("Proxied edit from {Sender} for event {OriginalEvent}", evt.Sender, editedEventId);
    }

    public async Task HandleRedaction(MatrixEvent evt)
    {
        var redactedEventId = evt.RedactedEventId;
        if (redactedEventId == null) return;

        var proxiedMsg = await _repo.GetMessageByOriginal(redactedEventId);
        if (proxiedMsg == null) return;

        if (proxiedMsg.SenderMxid != evt.Sender) return;

        try
        {
            var txnId = $"pk_redact_cascade_{evt.EventId}_{Guid.NewGuid():N}";
            await _api.RedactEvent(proxiedMsg.RoomId, proxiedMsg.ProxiedEventId, "Original message deleted", txnId);
        }
        catch (HttpRequestException ex)
        {
            _logger.Warning(ex, "Failed to cascade-redact proxied event {ProxiedEvent}", proxiedMsg.ProxiedEventId);
        }

        // Always clean up the DB record even if redaction API call failed
        await _repo.DeleteMessage(proxiedMsg.ProxiedEventId);

        _logger.Information("Cascade-redacted proxied event {ProxiedEvent} due to original {OriginalEvent} being redacted",
            proxiedMsg.ProxiedEventId, redactedEventId);
    }

    public async Task HandleReactionDelete(MatrixEvent evt)
    {
        if (evt.ReactionKey != "\u274c") return; // cross mark

        var targetEventId = evt.ReactionTargetEventId;
        if (targetEventId == null) return;

        var proxiedMsg = await _repo.GetMessage(targetEventId);
        if (proxiedMsg == null) return;

        if (proxiedMsg.SenderMxid != evt.Sender) return;

        try
        {
            var txnId = $"pk_react_delete_{evt.EventId}_{Guid.NewGuid():N}";
            await _api.RedactEvent(proxiedMsg.RoomId, proxiedMsg.ProxiedEventId, "Deleted by sender reaction", txnId);
        }
        catch (HttpRequestException ex)
        {
            _logger.Warning(ex, "Failed to reaction-delete proxied event {ProxiedEvent}", proxiedMsg.ProxiedEventId);
        }

        await _repo.DeleteMessage(proxiedMsg.ProxiedEventId);

        _logger.Information("Reaction-deleted proxied event {ProxiedEvent} by sender {Sender}",
            proxiedMsg.ProxiedEventId, evt.Sender);
    }

    private static bool IsUnlatch(string message) => message.StartsWith(@"\\") && !message.StartsWith(@"\\\");
    private static bool IsDisableAutoproxy(string message) => message.StartsWith(@"\\\");
}
