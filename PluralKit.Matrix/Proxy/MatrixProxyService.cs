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
    private readonly MatrixConfig _config;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public MatrixProxyService(MatrixApiClient api, MatrixRepository repo, VirtualUserService virtualUsers,
        ProxyMatcher matcher, ModelRepository coreRepo, MatrixConfig config,
        IClock clock, ILogger logger)
    {
        _api = api;
        _repo = repo;
        _virtualUsers = virtualUsers;
        _matcher = matcher;
        _coreRepo = coreRepo;
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

        // Get message context from database
        var ctx = await _repo.GetMessageContext(senderMxid, roomId);

        // No system linked
        if (ctx.SystemId == null)
            return false;

        // Room is blacklisted
        if (ctx.InBlacklist)
            return false;

        // Get autoproxy settings for this room
        var autoproxySettings = await _repo.GetAutoproxySettings(ctx.SystemId.Value, roomId);

        // Check for unlatch (\\)
        if (autoproxySettings.AutoproxyMode == AutoproxyMode.Latch && IsUnlatch(messageContent))
        {
            await _repo.UpdateAutoproxy(ctx.SystemId.Value, roomId, new AutoproxyPatch
            {
                AutoproxyMember = null
            });
            return false;
        }

        // Check for disable autoproxy (\\\)
        if (IsDisableAutoproxy(messageContent))
        {
            await _repo.UpdateAutoproxy(ctx.SystemId.Value, roomId, new AutoproxyPatch
            {
                AutoproxyMode = AutoproxyMode.Off
            });
            return false;
        }

        // Get proxy members
        var members = (await _repo.GetProxyMembers(senderMxid)).ToList();
        if (members.Count == 0) return false;

        // Try to match proxy tags
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
                _logger.Debug("Proxy check failed for {Sender}: {Message}", senderMxid, ex.Message);
            return false;
        }

        // Execute the proxy
        await ExecuteProxy(evt, ctx, autoproxySettings, match);
        return true;
    }

    private async Task ExecuteProxy(MatrixEvent trigger, MessageContext ctx, AutoproxySettings autoproxySettings, ProxyMatch match)
    {
        var member = await _coreRepo.GetMember(match.Member.Id);
        var memberHid = member?.Hid ?? "unknown";
        var virtualMxid = $"@_pk_{memberHid}:{_config.ServerName}";
        var roomId = trigger.RoomId;

        // Ensure the virtual user is registered, has the right profile, and is in the room
        await _virtualUsers.EnsureRegistered(match.Member, memberHid, ctx);
        await _virtualUsers.EnsureJoined(virtualMxid, roomId);

        // Send the proxied message
        var content = match.ProxyContent ?? match.Content ?? "";
        var txnId = $"pk_{trigger.EventId}_{Guid.NewGuid():N}";

        var proxyEventId = await _api.SendMessage(roomId, virtualMxid, content, null, txnId);

        // Try to redact the original message (graceful failure if no permission)
        var redactTxnId = $"pk_redact_{trigger.EventId}_{Guid.NewGuid():N}";
        await _api.RedactEvent(roomId, trigger.EventId, "Proxied by PluralKit", redactTxnId);

        // Store the proxied message record
        await _repo.AddMessage(new MatrixMessage
        {
            ProxiedEventId = proxyEventId,
            OriginalEventId = trigger.EventId,
            RoomId = roomId,
            Member = match.Member.Id,
            SenderMxid = trigger.Sender,
        });

        // Update autoproxy latch if applicable
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

        // Find the proxied message for the original event
        var proxiedMsg = await _repo.GetMessageByOriginal(editedEventId);
        if (proxiedMsg == null) return;

        // Verify the editor is the original sender
        if (proxiedMsg.SenderMxid != evt.Sender) return;

        var member = await _coreRepo.GetMember(proxiedMsg.Member!.Value);
        var memberHid = member?.Hid ?? "unknown";
        var virtualMxid = $"@_pk_{memberHid}:{_config.ServerName}";

        // Get the new content
        var newBody = (string?)evt.NewContent?["body"] ?? evt.Body ?? "";
        var newFormattedBody = (string?)evt.NewContent?["formatted_body"];

        // Send edit as virtual user
        var txnId = $"pk_edit_{evt.EventId}_{Guid.NewGuid():N}";
        await _api.SendEdit(evt.RoomId, virtualMxid, proxiedMsg.ProxiedEventId, newBody, newFormattedBody, txnId);

        _logger.Information("Proxied edit from {Sender} for event {OriginalEvent}", evt.Sender, editedEventId);
    }

    public async Task HandleRedaction(MatrixEvent evt)
    {
        var redactedEventId = evt.RedactedEventId;
        if (redactedEventId == null) return;

        // Check if the redacted event was an original message that we proxied
        var proxiedMsg = await _repo.GetMessageByOriginal(redactedEventId);
        if (proxiedMsg == null) return;

        // Verify the person redacting is the original sender
        if (proxiedMsg.SenderMxid != evt.Sender) return;

        // Redact the proxied message too
        var txnId = $"pk_redact_cascade_{evt.EventId}_{Guid.NewGuid():N}";
        await _api.RedactEvent(proxiedMsg.RoomId, proxiedMsg.ProxiedEventId, "Original message deleted", txnId);

        // Remove from our tracking
        await _repo.DeleteMessage(proxiedMsg.ProxiedEventId);

        _logger.Information("Cascade-redacted proxied event {ProxiedEvent} due to original {OriginalEvent} being redacted",
            proxiedMsg.ProxiedEventId, redactedEventId);
    }

    public async Task HandleReactionDelete(MatrixEvent evt)
    {
        // Check for cross-mark reaction on a proxied message
        if (evt.ReactionKey != "\u274c") return; // ❌

        var targetEventId = evt.ReactionTargetEventId;
        if (targetEventId == null) return;

        // Check if this is a proxied message
        var proxiedMsg = await _repo.GetMessage(targetEventId);
        if (proxiedMsg == null) return;

        // Only the original sender can delete via reaction
        if (proxiedMsg.SenderMxid != evt.Sender) return;

        // Redact the proxied message
        var txnId = $"pk_react_delete_{evt.EventId}_{Guid.NewGuid():N}";
        await _api.RedactEvent(proxiedMsg.RoomId, proxiedMsg.ProxiedEventId, "Deleted by sender reaction", txnId);

        await _repo.DeleteMessage(proxiedMsg.ProxiedEventId);

        _logger.Information("Reaction-deleted proxied event {ProxiedEvent} by sender {Sender}",
            proxiedMsg.ProxiedEventId, evt.Sender);
    }

    private static bool IsUnlatch(string message) => message.StartsWith(@"\\");
    private static bool IsDisableAutoproxy(string message) => message.StartsWith(@"\\\");
}
