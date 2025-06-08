using NodaTime;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot;

public class CommandMessageService
{
    private readonly RedisService _redis;
    private readonly ModelRepository _repo;
    private readonly ILogger _logger;
    private static readonly TimeSpan CommandMessageRetention = TimeSpan.FromHours(24);

    public CommandMessageService(RedisService redis, ModelRepository repo, IClock clock, ILogger logger)
    {
        _redis = redis;
        _repo = repo;
        _logger = logger.ForContext<CommandMessageService>();
    }

    public async Task<CommandMessage?> GetCommandMessage(ulong messageId)
    {
        var repoMsg = await _repo.GetCommandMessage(messageId);
        if (repoMsg != null)
            return new CommandMessage(repoMsg.Sender, repoMsg.Channel, repoMsg.Guild);

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