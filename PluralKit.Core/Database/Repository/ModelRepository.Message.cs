using Dapper;

using SqlKata;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task AddMessage(PKMessage msg)
    {
        var query = new Query("messages").AsInsert(new
        {
            mid = msg.Mid,
            guild = msg.Guild,
            channel = msg.Channel,
            member = msg.Member,
            sender = msg.Sender,
            original_mid = msg.OriginalMid
        });
        _logger.Debug("Stored message {@StoredMessage} in channel {Channel}", msg, msg.Channel);

        // "on conflict do nothing" in the (pretty rare) case of duplicate events coming in from Discord, which would lead to a DB error before
        return _db.ExecuteQuery(query, "on conflict do nothing", messages: true);
    }

    public Task<PKMessage?> GetMessage(ulong id)
        => _db.QueryFirst<PKMessage?>(new Query("messages").Where("mid", id), messages: true);

    public Task<IEnumerable<PKMessage>> GetMessagesBulk(IReadOnlyCollection<ulong> ids)
        => _db.Query<PKMessage>(new Query("messages").WhereIn("mid", ids.Select(id => (long)id).ToArray()));

    public async Task<FullMessage?> GetFullMessage(ulong id)
    {
        var rawMessage = await GetMessage(id);
        if (rawMessage == null) return null;

        var member = rawMessage.Member == null ? null : await GetMember(rawMessage.Member.Value);
        var system = member == null ? null : await GetSystem(member.System);

        return new FullMessage
        {
            Message = rawMessage,
            Member = member,
            System = system,
        };
    }

    public async Task DeleteMessage(ulong id)
    {
        var query = new Query("messages").AsDelete().Where("mid", id);
        var rowCount = await _db.ExecuteQuery(query, messages: true);
        if (rowCount > 0)
            _logger.Information("Deleted message {MessageId} from database", id);
    }

    public async Task DeleteMessagesBulk(IReadOnlyCollection<ulong> ids)
    {
        // Npgsql doesn't support ulongs in general - we hacked around it for plain ulongs but tbh not worth it for collections of ulong
        // Hence we map them to single longs, which *are* supported (this is ok since they're Technically (tm) stored as signed longs in the db anyway)
        var query = new Query("messages").AsDelete().WhereIn("mid", ids.Select(id => (long)id).ToArray());
        var rowCount = await _db.ExecuteQuery(query, messages: true);
        if (rowCount > 0)
            _logger.Information("Bulk deleted messages ({FoundCount} found) from database: {MessageIds}", rowCount,
                ids);
    }
}