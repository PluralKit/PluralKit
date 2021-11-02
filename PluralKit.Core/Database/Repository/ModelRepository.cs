using Serilog;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        private readonly ILogger _logger;
        private readonly IDatabase _db;
        private readonly DispatchService _dispatch;
        public ModelRepository(ILogger logger, IDatabase db, DispatchService dispatch)
        {
            _logger = logger.ForContext<ModelRepository>();
            _db = db;
            _dispatch = dispatch;
        }
    }
}