using System.Text;

using Dapper;

namespace PluralKit.Core
{
    public class UpdateQueryBuilder
    {
        private readonly string _table;
        private readonly string _condition;
        private readonly DynamicParameters _params = new DynamicParameters();

        private bool _hasFields = false;
        private readonly StringBuilder _setClause = new StringBuilder();

        public UpdateQueryBuilder(string table, string condition)
        {
            _table = table;
            _condition = condition;
        }

        public UpdateQueryBuilder WithConstant<T>(string name, T value)
        {
            _params.Add(name, value);
            return this;
        }

        public UpdateQueryBuilder With<T>(string columnName, T value)
        {
            _params.Add(columnName, value);

            if (_hasFields)
                _setClause.Append(", ");
            else _hasFields = true;

            _setClause.Append($"{columnName} = @{columnName}");
            return this;
        }

        public UpdateQueryBuilder With<T>(string columnName, Partial<T> partialValue)
        {
            return partialValue.IsPresent ? With(columnName, partialValue.Value) : this;
        }

        public (string Query, DynamicParameters Parameters) Build(string append = "")
        {
            var query = $"update {_table} set {_setClause} where {_condition} {append}";
            return (query, _params);
        }
    }
}