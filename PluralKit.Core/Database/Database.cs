using System.Data;
using System.Runtime.CompilerServices;

using App.Metrics;

using Dapper;

using NodaTime;

using Npgsql;

using Serilog;

using SqlKata;
using SqlKata.Compilers;

namespace PluralKit.Core;

internal class Database: IDatabase
{

    private readonly CoreConfig _config;
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;
    private readonly DbConnectionCountHolder _countHolder;
    private readonly DatabaseMigrator _migrator;
    private readonly string _connectionString;

    public Database(CoreConfig config, DbConnectionCountHolder countHolder, ILogger logger,
                    IMetrics metrics, DatabaseMigrator migrator)
    {
        _config = config;
        _countHolder = countHolder;
        _metrics = metrics;
        _migrator = migrator;
        _logger = logger.ForContext<Database>();

        _connectionString = new NpgsqlConnectionStringBuilder(_config.Database)
        {
            Pooling = true,
            Enlist = false,
            NoResetOnClose = true,

            // Lower timeout than default (15s -> 2s), should ideally fail-fast instead of hanging
            Timeout = 2
        }.ConnectionString;
    }

    private static readonly PostgresCompiler _compiler = new();

    public static void InitStatic()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        // Dapper by default tries to pass ulongs to Npgsql, which rejects them since PostgreSQL technically
        // doesn't support unsigned types on its own.
        // Instead we add a custom mapper to encode them as signed integers instead, converting them back and forth.
        SqlMapper.RemoveTypeMap(typeof(ulong));
        SqlMapper.AddTypeHandler(new UlongEncodeAsLongHandler());
        SqlMapper.AddTypeHandler(new UlongArrayHandler());

        NpgsqlConnection.GlobalTypeMapper.UseNodaTime();
        // With the thing we add above, Npgsql already handles NodaTime integration
        // This makes Dapper confused since it thinks it has to convert it anyway and doesn't understand the types
        // So we add a custom type handler that literally just passes the type through to Npgsql
        SqlMapper.AddTypeHandler(new PassthroughTypeHandler<Instant>());
        SqlMapper.AddTypeHandler(new PassthroughTypeHandler<LocalDate>());

        // Add ID types to Dapper
        SqlMapper.AddTypeHandler(new NumericIdHandler<SystemId, int>(i => new SystemId(i)));
        SqlMapper.AddTypeHandler(new NumericIdHandler<MemberId, int>(i => new MemberId(i)));
        SqlMapper.AddTypeHandler(new NumericIdHandler<SwitchId, int>(i => new SwitchId(i)));
        SqlMapper.AddTypeHandler(new NumericIdHandler<GroupId, int>(i => new GroupId(i)));
        SqlMapper.AddTypeHandler(new NumericIdArrayHandler<SystemId, int>(i => new SystemId(i)));
        SqlMapper.AddTypeHandler(new NumericIdArrayHandler<MemberId, int>(i => new MemberId(i)));
        SqlMapper.AddTypeHandler(new NumericIdArrayHandler<SwitchId, int>(i => new SwitchId(i)));
        SqlMapper.AddTypeHandler(new NumericIdArrayHandler<GroupId, int>(i => new GroupId(i)));

        // Register our custom types to Npgsql
        // Without these it'll still *work* but break at the first launch + probably cause other small issues
        NpgsqlConnection.GlobalTypeMapper.MapComposite<ProxyTag>("proxy_tag");
        NpgsqlConnection.GlobalTypeMapper.MapEnum<PrivacyLevel>("privacy_level");
    }

    public async Task<IPKConnection> Obtain()
    {
        // Mark the request (for a handle, I guess) in the metrics
        _metrics.Measure.Meter.Mark(CoreMetrics.DatabaseRequests);

        // Create a connection and open it
        // We wrap it in PKConnection for tracing purposes
        var conn = new PKConnection(new NpgsqlConnection(_connectionString), _countHolder, _logger, _metrics);
        await conn.OpenAsync();
        return conn;
    }

    public async Task ApplyMigrations()
    {
        using var conn = await Obtain();
        await _migrator.ApplyMigrations(conn);
    }

    private class PassthroughTypeHandler<T>: SqlMapper.TypeHandler<T>
    {
        public override void SetValue(IDbDataParameter parameter, T value) => parameter.Value = value;
        public override T Parse(object value) => (T)value;
    }

    private class UlongEncodeAsLongHandler: SqlMapper.TypeHandler<ulong>
    {
        public override ulong Parse(object value) =>
            // Cast to long to unbox, then to ulong (???)
            (ulong)(long)value;

        public override void SetValue(IDbDataParameter parameter, ulong value) => parameter.Value = (long)value;
    }

    private class UlongArrayHandler: SqlMapper.TypeHandler<ulong[]>
    {
        public override void SetValue(IDbDataParameter parameter, ulong[] value) => parameter.Value = Array.ConvertAll(value, i => (long)i);

        public override ulong[] Parse(object value) => Array.ConvertAll((long[])value, i => (ulong)i);
    }

    private class NumericIdHandler<T, TInner>: SqlMapper.TypeHandler<T>
        where T : INumericId<T, TInner>
        where TInner : IEquatable<TInner>, IComparable<TInner>
    {
        private readonly Func<TInner, T> _factory;

        public NumericIdHandler(Func<TInner, T> factory)
        {
            _factory = factory;
        }

        public override void SetValue(IDbDataParameter parameter, T value) => parameter.Value = value.Value;

        public override T Parse(object value) => _factory((TInner)value);
    }

    private class NumericIdArrayHandler<T, TInner>: SqlMapper.TypeHandler<T[]>
        where T : INumericId<T, TInner>
        where TInner : IEquatable<TInner>, IComparable<TInner>
    {
        private readonly Func<TInner, T> _factory;

        public NumericIdArrayHandler(Func<TInner, T> factory)
        {
            _factory = factory;
        }

        public override void SetValue(IDbDataParameter parameter, T[] value) => parameter.Value = Array.ConvertAll(value, v => v.Value);

        public override T[] Parse(object value) => Array.ConvertAll((TInner[])value, v => _factory(v));
    }

    public async Task Execute(Func<IPKConnection, Task> func)
    {
        await using var conn = await Obtain();
        await func(conn);
    }

    public async Task<T> Execute<T>(Func<IPKConnection, Task<T>> func)
    {
        await using var conn = await Obtain();
        return await func(conn);
    }

    public async IAsyncEnumerable<T> Execute<T>(Func<IPKConnection, IAsyncEnumerable<T>> func)
    {
        await using var conn = await Obtain();

        await foreach (var val in func(conn))
            yield return val;
    }

    public async Task<int> ExecuteQuery(Query q, string extraSql = "", [CallerMemberName] string queryName = "")
    {
        var query = _compiler.Compile(q);
        using var conn = await Obtain();
        using (_metrics.Measure.Timer.Time(CoreMetrics.DatabaseQuery, new MetricTags("Query", queryName)))
            return await conn.ExecuteAsync(query.Sql + $" {extraSql}", query.NamedBindings);
    }

    public async Task<int> ExecuteQuery(IPKConnection? conn, Query q, string extraSql = "", [CallerMemberName] string queryName = "")
    {
        if (conn == null)
            return await ExecuteQuery(q, extraSql, queryName);

        var query = _compiler.Compile(q);
        using (_metrics.Measure.Timer.Time(CoreMetrics.DatabaseQuery, new MetricTags("Query", queryName)))
            return await conn.ExecuteAsync(query.Sql + $" {extraSql}", query.NamedBindings);
    }

    public async Task<T> QueryFirst<T>(Query q, string extraSql = "", [CallerMemberName] string queryName = "")
    {
        var query = _compiler.Compile(q);
        using var conn = await Obtain();
        using (_metrics.Measure.Timer.Time(CoreMetrics.DatabaseQuery, new MetricTags("Query", queryName)))
            return await conn.QueryFirstOrDefaultAsync<T>(query.Sql + $" {extraSql}", query.NamedBindings);
    }

    public async Task<T> QueryFirst<T>(IPKConnection? conn, Query q, string extraSql = "", [CallerMemberName] string queryName = "")
    {
        if (conn == null)
            return await QueryFirst<T>(q, extraSql, queryName);

        var query = _compiler.Compile(q);
        using (_metrics.Measure.Timer.Time(CoreMetrics.DatabaseQuery, new MetricTags("Query", queryName)))
            return await conn.QueryFirstOrDefaultAsync<T>(query.Sql + $" {extraSql}", query.NamedBindings);
    }

    public async Task<IEnumerable<T>> Query<T>(Query q, [CallerMemberName] string queryName = "")
    {
        var query = _compiler.Compile(q);
        using var conn = await Obtain();
        using (_metrics.Measure.Timer.Time(CoreMetrics.DatabaseQuery, new MetricTags("Query", queryName)))
            return await conn.QueryAsync<T>(query.Sql, query.NamedBindings);
    }

    public async IAsyncEnumerable<T> QueryStream<T>(Query q, [CallerMemberName] string queryName = "")
    {
        var query = _compiler.Compile(q);
        using var conn = await Obtain();
        using (_metrics.Measure.Timer.Time(CoreMetrics.DatabaseQuery, new MetricTags("Query", queryName)))
            await foreach (var val in conn.QueryStreamAsync<T>(query.Sql, query.NamedBindings))
                yield return val;
    }

    // the procedures (message_context and proxy_members, as of writing) have their own metrics tracking elsewhere
    // still, including them here for consistency

    public async Task<T> QuerySingleProcedure<T>(string queryName, object param)
    {
        using var conn = await Obtain();
        return await conn.QueryFirstAsync<T>(queryName, param, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<T>> QueryProcedure<T>(string queryName, object param)
    {
        using var conn = await Obtain();
        return await conn.QueryAsync<T>(queryName, param, commandType: CommandType.StoredProcedure);
    }
}