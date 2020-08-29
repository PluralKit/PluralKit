using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public Task<MessageContext> GetMessageContext(IPKConnection conn, ulong account, ulong guild, ulong channel)
        {
            return conn.QueryFirstAsync<MessageContext>("message_context", 
                new { account_id = account, guild_id = guild, channel_id = channel }, 
                commandType: CommandType.StoredProcedure);
        }  
        
        public Task<IEnumerable<ProxyMember>> GetProxyMembers(IPKConnection conn, ulong account, ulong guild)
        {
            return conn.QueryAsync<ProxyMember>("proxy_members", 
                new { account_id = account, guild_id = guild }, 
                commandType: CommandType.StoredProcedure);
        }  
    }
}