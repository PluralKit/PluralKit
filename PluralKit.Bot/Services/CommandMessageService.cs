using NodaTime;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot;

public class CommandMessageService
{
    private readonly IClock _clock;
    private readonly IDatabase _db;
    private readonly ILogger _logger;
    private readonly ModelRepository _repo;

    public CommandMessageService(IDatabase db, ModelRepository repo, IClock clock, ILogger logger)
    {
        _db = db;
        _repo = repo;
        _clock = clock;
        _logger = logger.ForContext<CommandMessageService>();
    }

    public async Task RegisterMessage(ulong messageId, ulong channelId, ulong authorId)
    {
        _logger.Debug(
            "Registering command response {MessageId} from author {AuthorId} in {ChannelId}",
            messageId, authorId, channelId
        );
        await _repo.SaveCommandMessage(messageId, channelId, authorId);
    }

    public async Task<CommandMessage?> GetCommandMessage(ulong messageId) =>
        await _repo.GetCommandMessage(messageId);
}