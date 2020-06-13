using System;
using System.Threading.Tasks;

namespace PluralKit.Core
{
    public static class DatabaseExt
    {
        public static async Task Execute(this IDatabase db, Func<IPKConnection, Task> func)
        {
            await using var conn = await db.Obtain();
            await func(conn);
        }
        
        public static async Task<T> Execute<T>(this IDatabase db, Func<IPKConnection, Task<T>> func)
        {
            await using var conn = await db.Obtain();
            return await func(conn);
        }
    }
}