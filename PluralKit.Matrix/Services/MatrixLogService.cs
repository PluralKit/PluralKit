using Serilog;

namespace PluralKit.Matrix;

public class MatrixLogService
{
    private readonly MatrixApiClient _api;
    private readonly MatrixRepository _repo;
    private readonly MatrixConfig _config;
    private readonly ILogger _logger;

    public MatrixLogService(MatrixApiClient api, MatrixRepository repo, MatrixConfig config, ILogger logger)
    {
        _api = api;
        _repo = repo;
        _config = config;
        _logger = logger.ForContext<MatrixLogService>();
    }

    public async Task LogProxy(string sourceRoomId, string memberName, string proxyEventId)
    {
        var logRoom = await _repo.GetLogRoom(sourceRoomId);
        if (logRoom == null) return;

        var botMxid = $"@{_config.BotLocalpart}:{_config.ServerName}";

        try
        {
            // Ensure bot is in the log room
            if (!await _api.JoinRoom(logRoom, botMxid))
                return;

            var txnId = $"pk_log_{proxyEventId}_{Guid.NewGuid():N}";
            await _api.SendMessage(logRoom, botMxid,
                $"**{memberName}** proxied a message in `{sourceRoomId}`", null, txnId);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to log proxy event {EventId} to log room {LogRoom}", proxyEventId, logRoom);
        }
    }
}
