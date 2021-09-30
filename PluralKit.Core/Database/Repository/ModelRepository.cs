using Serilog;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        private readonly ILogger _logger;
        private readonly IDatabase _db;
        public ModelRepository(ILogger logger, IDatabase db)
        {
            _logger = logger.ForContext<ModelRepository>();
            _db = db;
        }
    }
}