using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public static class DatabaseFunctionsExt
    {
        public static Task<MessageContext> QueryMessageContext(this IPKConnection conn, ulong account, ulong guild, ulong channel)
        {
            return conn.QueryFirstAsync<MessageContext>("message_context", 
                new { account_id = account, guild_id = guild, channel_id = channel }, 
                commandType: CommandType.StoredProcedure);
        }  
        
        public static Task<IEnumerable<ProxyMember>> QueryProxyMembers(this IPKConnection conn, ulong account, ulong guild)
        {
            return conn.QueryAsync<ProxyMember>("proxy_members", 
                new { account_id = account, guild_id = guild }, 
                commandType: CommandType.StoredProcedure);
        }  
    }
}