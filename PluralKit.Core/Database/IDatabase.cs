using System.Runtime.CompilerServices;

using SqlKata;

namespace PluralKit.Core;

public interface IDatabase
{
    Task ApplyMigrations();
    Task<IPKConnection> Obtain();
    Task Execute(Func<IPKConnection, Task> func);
    Task<T> Execute<T>(Func<IPKConnection, Task<T>> func);
    IAsyncEnumerable<T> Execute<T>(Func<IPKConnection, IAsyncEnumerable<T>> func);
    Task<int> ExecuteQuery(Query q, string extraSql = "", [CallerMemberName] string queryName = "");

    Task<int> ExecuteQuery(IPKConnection? conn, Query q, string extraSql = "",
                           [CallerMemberName] string queryName = "");

    Task<T> QueryFirst<T>(Query q, string extraSql = "", [CallerMemberName] string queryName = "");

    Task<T> QueryFirst<T>(IPKConnection? conn, Query q, string extraSql = "",
                          [CallerMemberName] string queryName = "");

    Task<IEnumerable<T>> Query<T>(Query q, [CallerMemberName] string queryName = "");
    IAsyncEnumerable<T> QueryStream<T>(Query q, [CallerMemberName] string queryName = "");
    Task<T> QuerySingleProcedure<T>(string queryName, object param);
    Task<IEnumerable<T>> QueryProcedure<T>(string queryName, object param);
}