using SqlKata;

namespace PluralKit.Core;

internal class QueryPatchWrapper
{
    private readonly Dictionary<string, object> _dict = new();

    public QueryPatchWrapper With<T>(string columnName, Partial<T> partialValue)
    {
        if (partialValue.IsPresent)
            _dict.Add(columnName, partialValue);

        return this;
    }

    public Query ToQuery(Query q)
    {
        try
        {
            return q.AsUpdate(_dict);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidPatchException();
        }
    }
}

internal static class SqlKataExtensions
{
    internal static Query ApplyPatch(this Query query, Func<QueryPatchWrapper, QueryPatchWrapper> func)
        => func(new QueryPatchWrapper()).ToQuery(query);
}

public class InvalidPatchException: Exception { }