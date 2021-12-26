using Serilog;

namespace Myriad.Gateway.Limit;

public class TwilightGatewayRatelimiter: IGatewayRatelimiter
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly ILogger _logger;
    private readonly string _url;

    public TwilightGatewayRatelimiter(ILogger logger, string url)
    {
        _url = url;
        _logger = logger.ForContext<TwilightGatewayRatelimiter>();
    }

    public async Task Identify(int shard)
    {
        while (true)
            try
            {
                _logger.Information("Shard {ShardId}: Requesting identify at gateway queue {GatewayQueueUrl}",
                    shard, _url);
                await _httpClient.GetAsync(_url + "?shard=" + shard);
                return;
            }
            catch (TaskCanceledException)
            {
                _logger.Warning("Shard {ShardId}: Gateway queue timed out, retrying", shard);
            }
    }
}