using Dapper;
using NodaTime;
using System.Threading.Tasks;

namespace PluralKit.Core {
    //TODO: Method to mark reminders as unseen
    public partial class ModelRepository {
        public async Task AddReminder(IPKConnection conn, PKReminder reminder) {
            await conn.ExecuteAsync("insert into reminders(mid, channel, guild, member, system, seen) values (@Mid, @Channel, @Guild, @Member, @System, @Seen)", reminder);
        }

    }

    public class PKReminder {
        public ulong Mid { get; set; }
        public ulong Channel { get; set; }
        public ulong? Guild { get; set; }
        public MemberId? Member { get; set; }
        public SystemId System { get; set; }
        public bool Seen { get; set; }
        public Instant Timestamp { get; set; }
    }
}