using NodaTime;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot;

public class CommandMessageService
{
    private readonly RedisService _redis;
    private readonly ILogger _logger;
    private static readonly TimeSpan CommandMessageRetention = TimeSpan.FromHours(24);

    public CommandMessageService(RedisService redis, IClock clock, ILogger logger)
    {
        _redis = redis;
        _logger = logger.ForContext<CommandMessageService>();
    }

    public async Task RegisterMessage(ulong messageId, ulong channelId, ulong authorId)
    {
        _logger.Debug(
            "Registering command response {MessageId} from author {AuthorId} in {ChannelId}",
            messageId, authorId, channelId
        );

        await _redis.Connection.GetDatabase().StringSetAsync(messageId.ToString(), $"{authorId}-{channelId}", expiry: CommandMessageRetention);
    }

    public async Task<(ulong?, ulong?)> GetCommandMessage(ulong messageId)
    {
        var str = await _redis.Connection.GetDatabase().StringGetAsync(messageId.ToString());
        if (str.HasValue)
        {
            var split = ((string)str).Split("-");
            return (ulong.Parse(split[0]), ulong.Parse(split[1]));
        }
        return (null, null);
    }
}