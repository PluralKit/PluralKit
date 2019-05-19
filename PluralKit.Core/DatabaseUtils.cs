using System.Data;
using Dapper;
using NodaTime;
using Npgsql;

namespace PluralKit
{
    public static class DatabaseUtils
    {
        public static void Init()
        {
            // Dapper by default tries to pass ulongs to Npgsql, which rejects them since PostgreSQL technically
            // doesn't support unsigned types on its own.
            // Instead we add a custom mapper to encode them as signed integers instead, converting them back and forth.
            SqlMapper.RemoveTypeMap(typeof(ulong));
            SqlMapper.AddTypeHandler<ulong>(new UlongEncodeAsLongHandler());
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            // Also, use NodaTime. it's good.
            NpgsqlConnection.GlobalTypeMapper.UseNodaTime();
            // With the thing we add above, Npgsql already handles NodaTime integration
            // This makes Dapper confused since it thinks it has to convert it anyway and doesn't understand the types
            // So we add a custom type handler that literally just passes the type through to Npgsql
            SqlMapper.AddTypeHandler(new PassthroughTypeHandler<Instant>());
            SqlMapper.AddTypeHandler(new PassthroughTypeHandler<LocalDate>());
        }
        
        class UlongEncodeAsLongHandler : SqlMapper.TypeHandler<ulong>
        {
            public override ulong Parse(object value)
            {
                // Cast to long to unbox, then to ulong (???)
                return (ulong)(long)value;
            }

            public override void SetValue(IDbDataParameter parameter, ulong value)
            {
                parameter.Value = (long)value;
            }
        }

        class PassthroughTypeHandler<T> : SqlMapper.TypeHandler<T>
        {
            public override void SetValue(IDbDataParameter parameter, T value)
            {
                parameter.Value = value;
            }

            public override T Parse(object value)
            {
                return (T) value;
            }
        }
    }
}