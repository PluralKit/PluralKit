using System.Collections.Concurrent;

using Newtonsoft.Json.Linq;

using Serilog;

namespace PluralKit.Matrix;

public class MatrixEventHandler
{
    private readonly MatrixProxyService _proxy;
    private readonly MatrixCommandHandler _commands;
    private readonly MatrixConfig _config;
    private readonly ILogger _logger;

    // Rate-limit E2EE warnings to once per room per hour
    private readonly ConcurrentDictionary<string, DateTimeOffset> _encryptedWarnings = new();

    public MatrixEventHandler(MatrixProxyService proxy, MatrixCommandHandler commands,
        MatrixConfig config, ILogger logger)
    {
        _proxy = proxy;
        _commands = commands;
        _config = config;
        _logger = logger.ForContext<MatrixEventHandler>();
    }

    public async Task HandleEvent(JObject rawEvent)
    {
        var evt = MatrixEvent.FromJson(rawEvent);

        // Ignore events from our own virtual users (@_pk_*)
        if (evt.Sender.StartsWith("@_pk_") && evt.Sender.EndsWith($":{_config.ServerName}"))
            return;

        // Ignore events from the appservice bot user itself
        if (evt.Sender == $"@{_config.BotLocalpart}:{_config.ServerName}")
            return;

        switch (evt.Type)
        {
            case "m.room.message":
                await HandleMessage(evt);
                break;

            case "m.room.redaction":
                await _proxy.HandleRedaction(evt);
                break;

            case "m.reaction":
                await _proxy.HandleReactionDelete(evt);
                break;

            case "m.room.encrypted":
                HandleEncryptedEvent(evt);
                break;
        }
    }

    private void HandleEncryptedEvent(MatrixEvent evt)
    {
        // Rate-limit to once per room per hour to avoid log spam
        var now = DateTimeOffset.UtcNow;
        if (_encryptedWarnings.TryGetValue(evt.RoomId, out var lastWarning)
            && (now - lastWarning).TotalHours < 1)
            return;

        _encryptedWarnings[evt.RoomId] = now;
        _logger.Debug("Ignoring encrypted event {EventId} in {Room} — E2EE not supported. " +
            "PluralKit can only proxy in rooms with encryption disabled.", evt.EventId, evt.RoomId);
    }

    private async Task HandleMessage(MatrixEvent evt)
    {
        // Handle edits (m.replace relation)
        if (evt.IsEdit)
        {
            await _proxy.HandleEdit(evt);
            return;
        }

        // Only process text and media messages
        if (evt.MessageType != "m.text" && !evt.IsMedia) return;

        // Commands only apply to text messages
        if (evt.MessageType == "m.text")
        {
            var body = evt.Body;
            if (string.IsNullOrWhiteSpace(body)) return;

            var prefix = _config.Prefix;

            if (body.StartsWith(prefix + " ") || body == prefix)
            {
                var args = body.Length > prefix.Length ? body.Substring(prefix.Length + 1).Trim() : "";
                await _commands.HandleCommand(evt, args);
                return;
            }
        }

        // Try proxy matching
        await _proxy.HandleIncomingMessage(evt);
    }
}
