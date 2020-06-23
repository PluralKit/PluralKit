using System.Threading;

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
}