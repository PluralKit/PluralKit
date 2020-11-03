using System.Threading.Tasks;

using NodaTime;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    public class CommandMessageService
    {
        private static readonly Duration CommandMessageRetention = Duration.FromHours(2);
        
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly IClock _clock;
        private readonly ILogger _logger;
        
        public CommandMessageService(IDatabase db, ModelRepository repo, IClock clock, ILogger logger)
        {
            _db = db;
            _repo = repo;
            _clock = clock;
            _logger = logger;
        }

        public async Task RegisterMessage(ulong messageId, ulong authorId)
        {
            _logger.Debug("Registering command response {MessageId} from author {AuthorId}", messageId, authorId);
            await _db.Execute(conn => _repo.SaveCommandMessage(conn, messageId, authorId));
        }

        public async Task<CommandMessage> GetCommandMessage(IPKConnection conn, ulong messageId)
        {
            return await _repo.GetCommandMessage(conn, messageId);
        }

        public async Task CleanupOldMessages()
        {
            var deleteThresholdInstant = _clock.GetCurrentInstant() - CommandMessageRetention;
            var deleteThresholdSnowflake = DiscordUtils.InstantToSnowflake(deleteThresholdInstant);

            var deletedRows = await _db.Execute(conn => _repo.DeleteCommandMessagesBefore(conn, deleteThresholdSnowflake));
            
            _logger.Information("Pruned {DeletedRows} command messages older than retention {Retention} (older than {DeleteThresholdInstant} / {DeleteThresholdSnowflake})",
                deletedRows, CommandMessageRetention, deleteThresholdInstant, deleteThresholdSnowflake);
        }
    }
}