using PluralKit.Core;

using Serilog;

namespace PluralKit.Matrix;

public partial class MatrixRepository
{
    private readonly IDatabase _db;
    private readonly ILogger _logger;

    public MatrixRepository(IDatabase db, ILogger logger)
    {
        _db = db;
        _logger = logger.ForContext<MatrixRepository>();
    }
}
