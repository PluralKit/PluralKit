using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public async Task AddMessage(IPKConnection conn, PKMessage msg) {
            // "on conflict do nothing" in the (pretty rare) case of duplicate events coming in from Discord, which would lead to a DB error before
            await conn.ExecuteAsync("insert into messages(mid, guild, channel, member, sender, original_mid) values(@Mid, @Guild, @Channel, @Member, @Sender, @OriginalMid) on conflict do nothing", msg);
            _logger.Debug("Stored message {@StoredMessage} in channel {Channel}", msg, msg.Channel);
        }
        
        public async Task<FullMessage> GetMessage(IPKConnection conn, ulong id)
        {
            FullMessage Mapper(PKMessage msg, PKMember member, PKSystem system) =>
                new FullMessage {Message = msg, System = system, Member = member};

            var result = await conn.QueryAsync<PKMessage, PKMember, PKSystem, FullMessage>(
                "select messages.*, members.*, systems.* from messages, members, systems where (mid = @Id or original_mid = @Id) and messages.member = members.id and systems.id = members.system",
                Mapper, new {Id = id});
            return result.FirstOrDefault();
        }

        public async Task DeleteMessage(IPKConnection conn, ulong id)
        {
            var rowCount = await conn.ExecuteAsync("delete from messages where mid = @Id", new {Id = id});
            if (rowCount > 0)
                _logger.Information("Deleted message {MessageId} from database", id);
        }

        public async Task DeleteMessagesBulk(IPKConnection conn, IReadOnlyCollection<ulong> ids)
        {
            // Npgsql doesn't support ulongs in general - we hacked around it for plain ulongs but tbh not worth it for collections of ulong
            // Hence we map them to single longs, which *are* supported (this is ok since they're Technically (tm) stored as signed longs in the db anyway)
            var rowCount = await conn.ExecuteAsync("delete from messages where mid = any(@Ids)",
                new {Ids = ids.Select(id => (long) id).ToArray()});
            if (rowCount > 0)
                _logger.Information("Bulk deleted messages ({FoundCount} found) from database: {MessageIds}", rowCount,
                    ids);
        }
    }

    public class PKMessage
    {
        public ulong Mid { get; set; }
        public ulong? Guild { get; set; } // null value means "no data" (ie. from before this field being added)
        public ulong Channel { get; set; }
        public MemberId Member { get; set; }
        public ulong Sender { get; set; }
        public ulong? OriginalMid { get; set; }
    }
    
    public class FullMessage
    {
        public PKMessage Message;
        public PKMember Member;
        public PKSystem System;
    }
}