using Newtonsoft.Json.Linq;

using Serilog;

namespace PluralKit.Matrix;

public class MatrixEventHandler
{
    private readonly MatrixProxyService _proxy;
    private readonly MatrixCommandHandler _commands;
    private readonly MatrixConfig _config;
    private readonly ILogger _logger;

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
        }
    }

    private async Task HandleMessage(MatrixEvent evt)
    {
        // Handle edits (m.replace relation)
        if (evt.IsEdit)
        {
            await _proxy.HandleEdit(evt);
            return;
        }

        // Only process text messages
        if (evt.MessageType != "m.text") return;

        var body = evt.Body;
        if (string.IsNullOrWhiteSpace(body)) return;

        var prefix = _config.Prefix;

        // Check if it's a command
        if (body.StartsWith(prefix + " ") || body == prefix)
        {
            var args = body.Length > prefix.Length ? body.Substring(prefix.Length + 1).Trim() : "";
            await _commands.HandleCommand(evt, args);
            return;
        }

        // Try proxy matching
        await _proxy.HandleIncomingMessage(evt);
    }
}
