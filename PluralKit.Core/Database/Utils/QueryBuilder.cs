#nullable enable
using System;
using System.Text;

namespace PluralKit.Core
{
    public class QueryBuilder
    {
        private readonly string? _conflictField;
        private readonly string? _condition;
        private readonly StringBuilder _insertFragment = new StringBuilder();
        private readonly StringBuilder _valuesFragment = new StringBuilder();
        private readonly StringBuilder _updateFragment = new StringBuilder();
        private bool _firstInsert = true;
        private bool _firstUpdate = true;
        public QueryType Type { get; }
        public string Table { get; }

        private QueryBuilder(QueryType type, string table, string? conflictField, string? condition)
        {
            Type = type;
            Table = table;
            _conflictField = conflictField;
            _condition = condition;
        }
        
        public static QueryBuilder Insert(string table) => new QueryBuilder(QueryType.Insert, table,  null, null);
        public static QueryBuilder Update(string table, string condition) => new QueryBuilder(QueryType.Update, table, null, condition);
        public static QueryBuilder Upsert(string table, string conflictField) => new QueryBuilder(QueryType.Upsert, table, conflictField, null);

        public QueryBuilder Constant(string fieldName, string paramName)
        {
            if (_firstInsert) _firstInsert = false;
            else 
            {
                _insertFragment.Append(", ");
                _valuesFragment.Append(", ");
            }
            
            _insertFragment.Append(fieldName);
            _valuesFragment.Append(paramName);
            return this;
        }
        
        public QueryBuilder Variable(string fieldName, string paramName)
        {
            Constant(fieldName, paramName);
            
            if (_firstUpdate) _firstUpdate = false;
            else _updateFragment.Append(", ");
            
            _updateFragment.Append(fieldName);
            _updateFragment.Append(" = ");
            _updateFragment.Append(paramName);
            return this;
        }

        public string Build(string? suffix = null)
        {
            if (_firstInsert)
                throw new ArgumentException("No fields have been added to the query.");
            
            StringBuilder query = new StringBuilder(Type switch
            {
                QueryType.Insert => $"insert into {Table} ({_insertFragment}) values ({_valuesFragment})",
                QueryType.Upsert => $"insert into {Table} ({_insertFragment}) values ({_valuesFragment}) on conflict ({_conflictField}) do update set {_updateFragment}",
                QueryType.Update => $"update {Table} set {_updateFragment}",
                _ => throw new ArgumentOutOfRangeException($"Unknown query type {Type}")
            });

            if (Type == QueryType.Update && _condition != null)
                query.Append($" where {_condition}");
            
            if (!string.IsNullOrEmpty(suffix))
                query.Append($" {suffix}");
            query.Append(";");

            return query.ToString();
        }

        public enum QueryType
        {
            Insert,
            Update,
            Upsert
        }
    }
}