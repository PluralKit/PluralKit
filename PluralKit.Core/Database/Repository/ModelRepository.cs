using Serilog;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        private readonly ILogger _logger;

        public ModelRepository(ILogger logger)
        {
            _logger = logger.ForContext<ILogger>()
                .ForContext("Elastic", "yes?");
        }
    }
}