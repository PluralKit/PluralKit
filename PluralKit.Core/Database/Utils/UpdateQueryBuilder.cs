using Dapper;

namespace PluralKit.Core;

public class UpdateQueryBuilder
{
    private readonly DynamicParameters _params = new();
    private readonly QueryBuilder _qb;

    private UpdateQueryBuilder(QueryBuilder qb)
    {
        _qb = qb;
    }

    public static UpdateQueryBuilder Insert(string table) => new(QueryBuilder.Insert(table));

    public static UpdateQueryBuilder Update(string table, string condition) =>
        new(QueryBuilder.Update(table, condition));

    public static UpdateQueryBuilder Upsert(string table, string conflictField) =>
        new(QueryBuilder.Upsert(table, conflictField));

    public UpdateQueryBuilder WithConstant<T>(string name, T value)
    {
        _params.Add(name, value);
        _qb.Constant(name, $"@{name}");
        return this;
    }

    public UpdateQueryBuilder With<T>(string columnName, T value)
    {
        _params.Add(columnName, value);
        _qb.Variable(columnName, $"@{columnName}");
        return this;
    }

    public UpdateQueryBuilder With<T>(string columnName, Partial<T> partialValue) =>
        partialValue.IsPresent ? With(columnName, partialValue.Value) : this;

    public (string Query, DynamicParameters Parameters) Build(string suffix = "") => (_qb.Build(suffix), _params);
}