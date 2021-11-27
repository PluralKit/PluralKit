using Serilog;

namespace PluralKit.Core;

public partial class ModelRepository
{
    private readonly IDatabase _db;
    private readonly DispatchService _dispatch;
    private readonly ILogger _logger;

    public ModelRepository(ILogger logger, IDatabase db, DispatchService dispatch)
    {
        _logger = logger.ForContext<ModelRepository>();
        _db = db;
        _dispatch = dispatch;
    }
}