using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using Serilog;

namespace PluralKit.Matrix;

[ApiController]
public class AppServiceController : ControllerBase
{
    private readonly MatrixConfig _config;
    private readonly MatrixEventHandler _eventHandler;
    private readonly MatrixRepository _repo;
    private readonly ILogger _logger;

    public AppServiceController(MatrixConfig config, MatrixEventHandler eventHandler,
        MatrixRepository repo, ILogger logger)
    {
        _config = config;
        _eventHandler = eventHandler;
        _repo = repo;
        _logger = logger.ForContext<AppServiceController>();
    }

    private bool ValidateHsToken()
    {
        var token = Request.Query["access_token"].FirstOrDefault();
        if (token == null && Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var auth = authHeader.FirstOrDefault();
            if (auth?.StartsWith("Bearer ") == true)
                token = auth.Substring(7);
        }
        return token == _config.HsToken;
    }

    /// <summary>Synapse pushes events here.</summary>
    [HttpPut("/_matrix/app/v1/transactions/{txnId}")]
    public async Task<IActionResult> HandleTransaction(string txnId, [FromBody] JObject body)
    {
        if (!ValidateHsToken())
            return Unauthorized(new { errcode = "M_UNKNOWN_TOKEN", error = "Invalid hs_token" });

        _logger.Debug("Received transaction {TxnId}", txnId);

        // Idempotency check
        if (await _repo.CheckTransaction(txnId))
        {
            _logger.Debug("Transaction {TxnId} already processed, skipping", txnId);
            return Ok(new { });
        }

        // Mark transaction as processed before handling events
        await _repo.StoreTransaction(txnId);

        // Process each event in the transaction
        var events = body["events"]?.ToObject<JArray>() ?? new JArray();
        foreach (var evt in events)
        {
            try
            {
                await _eventHandler.HandleEvent((JObject)evt);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error handling event in transaction {TxnId}: {EventType}",
                    txnId, evt["type"]?.Value<string>());
            }
        }

        return Ok(new { });
    }

    /// <summary>Synapse queries if we manage this user.</summary>
    [HttpGet("/_matrix/app/v1/users/{userId}")]
    public IActionResult QueryUser(string userId)
    {
        if (!ValidateHsToken())
            return Unauthorized(new { errcode = "M_UNKNOWN_TOKEN", error = "Invalid hs_token" });

        // Claim any user matching our namespace (@_pk_*)
        if (userId.StartsWith("@_pk_") && userId.Contains(':'))
        {
            _logger.Debug("Claiming user {UserId}", userId);
            return Ok(new { });
        }

        return NotFound(new { errcode = "M_NOT_FOUND", error = "User not managed by this appservice" });
    }

    /// <summary>Synapse queries if we manage this room alias.</summary>
    [HttpGet("/_matrix/app/v1/rooms/{roomAlias}")]
    public IActionResult QueryRoom(string roomAlias)
    {
        if (!ValidateHsToken())
            return Unauthorized(new { errcode = "M_UNKNOWN_TOKEN", error = "Invalid hs_token" });

        // We don't manage any room aliases
        return NotFound(new { errcode = "M_NOT_FOUND", error = "Room not managed by this appservice" });
    }
}
