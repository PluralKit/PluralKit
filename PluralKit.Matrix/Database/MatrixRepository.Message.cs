using Dapper;

using PluralKit.Core;

namespace PluralKit.Matrix;

public class MatrixMessage
{
    public string ProxiedEventId { get; set; } = "";
    public string? OriginalEventId { get; set; }
    public string RoomId { get; set; } = "";
    public MemberId? Member { get; set; }
    public string SenderMxid { get; set; } = "";
}

public partial class MatrixRepository
{
    public async Task AddMessage(MatrixMessage msg)
    {
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync(
            @"insert into matrix_messages (proxied_event_id, original_event_id, room_id, member, sender_mxid)
              values (@ProxiedEventId, @OriginalEventId, @RoomId, @Member, @SenderMxid)
              on conflict do nothing",
            msg);
        _logger.Debug("Stored Matrix message {ProxiedEventId} in room {RoomId}", msg.ProxiedEventId, msg.RoomId);
    }

    public async Task<MatrixMessage?> GetMessage(string proxiedEventId)
    {
        await using var conn = await _db.Obtain();
        return await conn.QueryFirstOrDefaultAsync<MatrixMessage>(
            "select proxied_event_id, original_event_id, room_id, member, sender_mxid from matrix_messages where proxied_event_id = @id",
            new { id = proxiedEventId });
    }

    public async Task<MatrixMessage?> GetMessageByOriginal(string originalEventId)
    {
        await using var conn = await _db.Obtain();
        return await conn.QueryFirstOrDefaultAsync<MatrixMessage>(
            "select proxied_event_id, original_event_id, room_id, member, sender_mxid from matrix_messages where original_event_id = @id",
            new { id = originalEventId });
    }

    public async Task DeleteMessage(string proxiedEventId)
    {
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync("delete from matrix_messages where proxied_event_id = @id",
            new { id = proxiedEventId });
    }

    public async Task<bool> CheckTransaction(string txnId)
    {
        await using var conn = await _db.Obtain();
        return await conn.QueryFirstOrDefaultAsync<bool>(
            "select exists(select 1 from matrix_transactions where txn_id = @txnId)",
            new { txnId });
    }

    public async Task StoreTransaction(string txnId)
    {
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync(
            "insert into matrix_transactions (txn_id) values (@txnId) on conflict do nothing",
            new { txnId });
    }
}
