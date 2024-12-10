namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task<MessageContext> GetMessageContext(ulong account, ulong guild, ulong channel, ulong thread)
        => _db.QuerySingleProcedure<MessageContext>("select * from message_context(@account, @guild, @channel, @thread)",
            new { account, guild, channel, thread });

    public Task<IEnumerable<ProxyMember>> GetProxyMembers(ulong account, ulong guild)
        => _db.QueryProcedure<ProxyMember>("select * from proxy_members(@account, @guild)", new { account, guild });
}