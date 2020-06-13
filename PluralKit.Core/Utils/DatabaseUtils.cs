using System.Data;
using System.Threading;

using Dapper;

namespace PluralKit.Core
{
    public class DbConnectionCountHolder
    {
        private int _connectionCount;
        public int ConnectionCount => _connectionCount;

        public void Increment()
        {
            Interlocked.Increment(ref _connectionCount);
        }

        public void Decrement()
        {
            Interlocked.Decrement(ref _connectionCount);
        }
    }

    public class PassthroughTypeHandler<T>: SqlMapper.TypeHandler<T>
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

    public class UlongEncodeAsLongHandler: SqlMapper.TypeHandler<ulong>
    {
        public override ulong Parse(object value)
        {
            // Cast to long to unbox, then to ulong (???)
            return (ulong) (long) value;
        }

        public override void SetValue(IDbDataParameter parameter, ulong value)
        {
            parameter.Value = (long) value;
        }
    }
}