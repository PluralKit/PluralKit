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

    public async Task RegisterMessage(ulong messageId, ulong guildId, ulong channelId, ulong authorId)
    {
        if (_redis.Connection == null) return;

        _logger.Debug(
            "Registering command response {MessageId} from author {AuthorId} in {ChannelId}",
            messageId, authorId, channelId
        );

        await _redis.Connection.GetDatabase().StringSetAsync("command_message:" + messageId.ToString(), $"{authorId}-{channelId}-{guildId}", expiry: CommandMessageRetention);
    }

    public async Task<CommandMessage?> GetCommandMessage(ulong messageId)
    {
        var str = await _redis.Connection.GetDatabase().StringGetAsync(messageId.ToString());
        if (str.HasValue)
        {
            var split = ((string)str).Split("-");
            return new CommandMessage(ulong.Parse(split[0]), ulong.Parse(split[1]), ulong.Parse(split[2]));
        }
        str = await _redis.Connection.GetDatabase().StringGetAsync("command_message:" + messageId.ToString());
        if (str.HasValue)
        {
            var split = ((string)str).Split("-");
            return new CommandMessage(ulong.Parse(split[0]), ulong.Parse(split[1]), ulong.Parse(split[2]));
        }
        return null;
    }
}

public record CommandMessage(ulong AuthorId, ulong ChannelId, ulong GuildId);