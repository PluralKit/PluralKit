using System.Data;
using System.Runtime.CompilerServices;

using App.Metrics;

using Dapper;

using SqlKata;

namespace PluralKit.Core;

internal partial class Database: IDatabase
{
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

    public async Task<T> QueryFirst<T>(string q, object param = null, [CallerMemberName] string queryName = "", bool messages = false)
    {
        using var conn = await Obtain(messages);
        using (_metrics.Measure.Timer.Time(CoreMetrics.DatabaseQuery, new MetricTags("Query", queryName)))
            return await conn.QueryFirstOrDefaultAsync<T>(q, param);
    }

    public async Task<int> ExecuteQuery(Query q, string extraSql = "", [CallerMemberName] string queryName = "", bool messages = false)
    {
        var query = _compiler.Compile(q);
        using var conn = await Obtain(messages);
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

    public async Task<T> QueryFirst<T>(Query q, string extraSql = "", [CallerMemberName] string queryName = "", bool messages = false)
    {
        var query = _compiler.Compile(q);
        using var conn = await Obtain(messages);
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
        {
            await foreach (var val in conn.QueryStreamAsync<T>(query.Sql, query.NamedBindings))
                yield return val;
        }
    }

    // the procedures (message_context and proxy_members, as of writing) have their own metrics tracking elsewhere
    // still, including them here for consistency

    public async Task<T> QuerySingleProcedure<T>(string queryName, object param)
    {
        using var conn = await Obtain();
        return await conn.QueryFirstAsync<T>(queryName, param, commandType: CommandType.Text);
    }

    public async Task<IEnumerable<T>> QueryProcedure<T>(string queryName, object param)
    {
        using var conn = await Obtain();
        return await conn.QueryAsync<T>(queryName, param);
    }
}