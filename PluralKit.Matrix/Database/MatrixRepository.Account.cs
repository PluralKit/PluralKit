using Dapper;

using PluralKit.Core;

namespace PluralKit.Matrix;

public partial class MatrixRepository
{
    public async Task<MessageContext> GetMessageContext(string mxid, string roomId)
    {
        await using var conn = await _db.Obtain();
        return await conn.QueryFirstAsync<MessageContext>(
            "select * from matrix_message_context(@mxid, @roomId)",
            new { mxid, roomId });
    }

    public async Task<IEnumerable<ProxyMember>> GetProxyMembers(string mxid)
    {
        await using var conn = await _db.Obtain();
        return await conn.QueryAsync<ProxyMember>(
            "select * from matrix_proxy_members(@mxid)", new { mxid });
    }

    public async Task<SystemId?> GetAccountSystem(string mxid)
    {
        await using var conn = await _db.Obtain();
        return await conn.QueryFirstOrDefaultAsync<int?>(
            "select system from matrix_accounts where mxid = @mxid", new { mxid }) is { } id
            ? new SystemId(id)
            : null;
    }

    public async Task LinkAccount(string mxid, SystemId systemId)
    {
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync(
            "insert into matrix_accounts (mxid, system) values (@mxid, @system) on conflict (mxid) do update set system = @system",
            new { mxid, system = systemId.Value });
        _logger.Information("Linked Matrix account {Mxid} to system {SystemId}", mxid, systemId);
    }

    public async Task UnlinkAccount(string mxid)
    {
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync("delete from matrix_accounts where mxid = @mxid", new { mxid });
        _logger.Information("Unlinked Matrix account {Mxid}", mxid);
    }
}
