using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using NodaTime;

namespace PluralKit {
    public class SystemStore {
        private DbConnectionFactory _conn;

        public SystemStore(DbConnectionFactory conn) {
            this._conn = conn;
        }
        
        public async Task<PKSystem> Create(string systemName = null) {
            // TODO: handle HID collision case
            var hid = Utils.GenerateHid();
            
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleAsync<PKSystem>("insert into systems (hid, name) values (@Hid, @Name) returning *", new { Hid = hid, Name = systemName });
        }

        public async Task Link(PKSystem system, ulong accountId) {
            // We have "on conflict do nothing" since linking an account when it's already linked to the same system is idempotent
            // This is used in import/export, although the pk;link command checks for this case beforehand
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("insert into accounts (uid, system) values (@Id, @SystemId) on conflict do nothing", new { Id = accountId, SystemId = system.Id });
        }
        
        public async Task Unlink(PKSystem system, ulong accountId) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from accounts where uid = @Id and system = @SystemId", new { Id = accountId, SystemId = system.Id });
        }

        public async Task<PKSystem> GetByAccount(ulong accountId) {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleOrDefaultAsync<PKSystem>("select systems.* from systems, accounts where accounts.system = systems.id and accounts.uid = @Id", new { Id = accountId });
        }

        public async Task<PKSystem> GetByHid(string hid) {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleOrDefaultAsync<PKSystem>("select * from systems where systems.hid = @Hid", new { Hid = hid.ToLower() });
        }

        public async Task<PKSystem> GetByToken(string token) {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleOrDefaultAsync<PKSystem>("select * from systems where token = @Token", new { Token = token });
        }
        
        public async Task<PKSystem> GetById(int id)
        {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleOrDefaultAsync<PKSystem>("select * from systems where id = @Id", new { Id = id });
        }

        public async Task Save(PKSystem system) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("update systems set name = @Name, description = @Description, tag = @Tag, avatar_url = @AvatarUrl, token = @Token, ui_tz = @UiTz where id = @Id", system);
        }

        public async Task Delete(PKSystem system) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from systems where id = @Id", system);
        }        

        public async Task<IEnumerable<ulong>> GetLinkedAccountIds(PKSystem system)
        {
            using (var conn = await _conn.Obtain())
                return await conn.QueryAsync<ulong>("select uid from accounts where system = @Id", new { Id = system.Id });
        }
    }

    public class MemberStore {
        private DbConnectionFactory _conn;

        public MemberStore(DbConnectionFactory conn) {
            this._conn = conn;
        }

        public async Task<PKMember> Create(PKSystem system, string name) {
            // TODO: handle collision
            var hid = Utils.GenerateHid();
            
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleAsync<PKMember>("insert into members (hid, system, name) values (@Hid, @SystemId, @Name) returning *", new {
                    Hid = hid,
                    SystemID = system.Id,
                    Name = name
                });
        }

        public async Task<PKMember> GetByHid(string hid) {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleOrDefaultAsync<PKMember>("select * from members where hid = @Hid", new { Hid = hid.ToLower() });
        }

        public async Task<PKMember> GetByName(PKSystem system, string name) {
            // QueryFirst, since members can (in rare cases) share names
            using (var conn = await _conn.Obtain())
                return await conn.QueryFirstOrDefaultAsync<PKMember>("select * from members where lower(name) = lower(@Name) and system = @SystemID", new { Name = name, SystemID = system.Id });
        }

        public async Task<ICollection<PKMember>> GetUnproxyableMembers(PKSystem system) {
            return (await GetBySystem(system))
                .Where((m) => {
                    var proxiedName = $"{m.Name} {system.Tag}";
                    return proxiedName.Length > 32 || proxiedName.Length < 2;
                }).ToList();
        }

        public async Task<IEnumerable<PKMember>> GetBySystem(PKSystem system) {
            using (var conn = await _conn.Obtain())
                return await conn.QueryAsync<PKMember>("select * from members where system = @SystemID", new { SystemID = system.Id });
        }

        public async Task Save(PKMember member) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("update members set name = @Name, description = @Description, color = @Color, avatar_url = @AvatarUrl, birthday = @Birthday, pronouns = @Pronouns, prefix = @Prefix, suffix = @Suffix where id = @Id", member);
        }

        public async Task Delete(PKMember member) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from members where id = @Id", member);
        }

        public async Task<int> MessageCount(PKMember member)
        {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleAsync<int>("select count(*) from messages where member = @Id", member);
        }

        public async Task<int> MemberCount(PKSystem system)
        {
            using (var conn = await _conn.Obtain())
                return await conn.ExecuteScalarAsync<int>("select count(*) from members where system = @Id", system);
        }
    }

    public class MessageStore {
        public struct PKMessage
        {
            public ulong Mid;
            public ulong Channel;
            public ulong Sender;
        }
        public class StoredMessage
        {
            public PKMessage Message;
            public PKMember Member;
            public PKSystem System;
        }

        private DbConnectionFactory _conn;

        public MessageStore(DbConnectionFactory conn) {
            this._conn = conn;
        }

        public async Task Store(ulong senderId, ulong messageId, ulong channelId, PKMember member) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("insert into messages(mid, channel, member, sender) values(@MessageId, @ChannelId, @MemberId, @SenderId)", new {
                    MessageId = messageId,
                    ChannelId = channelId,
                    MemberId = member.Id,
                    SenderId = senderId
                });      
        }

        public async Task<StoredMessage> Get(ulong id)
        {
            using (var conn = await _conn.Obtain())
                return (await conn.QueryAsync<PKMessage, PKMember, PKSystem, StoredMessage>("select messages.*, members.*, systems.* from messages, members, systems where mid = @Id and messages.member = members.id and systems.id = members.system", (msg, member, system) => new StoredMessage
                {
                    Message = msg,
                    System = system,
                    Member = member
                }, new { Id = id })).FirstOrDefault();
        }
        
        public async Task Delete(ulong id) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from messages where mid = @Id", new { Id = id });
        }
    }

    public class SwitchStore
    {
        private DbConnectionFactory _conn;

        public SwitchStore(DbConnectionFactory conn)
        {
            _conn = conn;
        }

        public async Task RegisterSwitch(PKSystem system, IEnumerable<PKMember> members)
        {
            // Use a transaction here since we're doing multiple executed commands in one
            using (var conn = await _conn.Obtain())
            using (var tx = conn.BeginTransaction())
            {
                // First, we insert the switch itself
                var sw = await conn.QuerySingleAsync<PKSwitch>("insert into switches(system) values (@System) returning *",
                    new {System = system.Id});
                
                // Then we insert each member in the switch in the switch_members table
                // TODO: can we parallelize this or send it in bulk somehow?
                foreach (var member in members)
                {
                    await conn.ExecuteAsync(
                        "insert into switch_members(switch, member) values(@Switch, @Member)",
                        new {Switch = sw.Id, Member = member.Id});
                }
                
                // Finally we commit the tx, since the using block will otherwise rollback it
                tx.Commit();
            }
        }

        public async Task<IEnumerable<PKSwitch>> GetSwitches(PKSystem system, int count = 9999999)
        {
            // TODO: refactor the PKSwitch data structure to somehow include a hydrated member list
            // (maybe when we get caching in?)
            using (var conn = await _conn.Obtain())
                return await conn.QueryAsync<PKSwitch>("select * from switches where system = @System order by timestamp desc limit @Count", new {System = system.Id, Count = count});
        }

        public async Task<IEnumerable<int>> GetSwitchMemberIds(PKSwitch sw)
        {
            using (var conn = await _conn.Obtain())
                return await conn.QueryAsync<int>("select member from switch_members where switch = @Switch order by switch_members.id",
                    new {Switch = sw.Id});
        }
        
        public async Task<IEnumerable<PKMember>> GetSwitchMembers(PKSwitch sw)
        {
            using (var conn = await _conn.Obtain())
                return await conn.QueryAsync<PKMember>(
                    "select * from switch_members, members where switch_members.member = members.id and switch_members.switch = @Switch order by switch_members.id",
                    new {Switch = sw.Id});
        }

        public async Task<PKSwitch> GetLatestSwitch(PKSystem system) => (await GetSwitches(system, 1)).FirstOrDefault();

        public async Task MoveSwitch(PKSwitch sw, Instant time)
        {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("update switches set timestamp = @Time where id = @Id",
                    new {Time = time, Id = sw.Id});
        }

        public async Task DeleteSwitch(PKSwitch sw)
        {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from switches where id = @Id", new {Id = sw.Id});
        }
        
        public struct SwitchListEntry
        {
            public ICollection<PKMember> Members;
            public Duration TimespanWithinRange;
        }

        public async Task<IEnumerable<SwitchListEntry>> GetTruncatedSwitchList(PKSystem system, Instant periodStart, Instant periodEnd)
        {
            // TODO: only fetch the necessary switches here
            // todo: this is in general not very efficient LOL
            // returns switches in chronological (newest first) order
            var switches = await GetSwitches(system);
            
            // we skip all switches that happened later than the range end, and taking all the ones that happened after the range start
            // *BUT ALSO INCLUDING* the last switch *before* the range (that partially overlaps the range period)
            var switchesInRange = switches.SkipWhile(sw => sw.Timestamp >= periodEnd).TakeWhileIncluding(sw => sw.Timestamp > periodStart).ToList();

            // query DB for all members involved in any of the switches above and collect into a dictionary for future use
            // this makes sure the return list has the same instances of PKMember throughout, which is important for the dictionary
            // key used in GetPerMemberSwitchDuration below
            Dictionary<int, PKMember> memberObjects;
            using (var conn = await _conn.Obtain())
            {
                memberObjects = (await conn.QueryAsync<PKMember>(
                        "select distinct members.* from members, switch_members where switch_members.switch = any(@Switches) and switch_members.member = members.id", // lol postgres specific `= any()` syntax
                        new {Switches = switchesInRange.Select(sw => sw.Id).ToList()}))
                    .ToDictionary(m => m.Id);
            }


            // we create the entry objects
            var outList = new List<SwitchListEntry>();

            // loop through every switch that *occurred* in-range and add it to the list
            // end time is the switch *after*'s timestamp - we cheat and start it out at the range end so the first switch in-range "ends" there instead of the one after's start point
            var endTime = periodEnd;
            foreach (var switchInRange in switchesInRange)
            {
                // find the start time of the switch, but clamp it to the range (only applicable to the Last Switch Before Range we include in the TakeWhileIncluding call above)
                var switchStartClamped = switchInRange.Timestamp;
                if (switchStartClamped < periodStart) switchStartClamped = periodStart;
                
                var span = endTime - switchStartClamped;
                outList.Add(new SwitchListEntry
                {
                    Members = (await GetSwitchMemberIds(switchInRange)).Select(id => memberObjects[id]).ToList(),
                    TimespanWithinRange = span
                });
                
                // next switch's end is this switch's start
                endTime = switchInRange.Timestamp;
            }

            return outList;
        }

        public struct PerMemberSwitchDuration
        {
            public Dictionary<PKMember, Duration> MemberSwitchDurations;
            public Duration NoFronterDuration;
        }

        public async Task<PerMemberSwitchDuration> GetPerMemberSwitchDuration(PKSystem system, Instant periodStart,
            Instant periodEnd)
        {
            var dict = new Dictionary<PKMember, Duration>();
            
            var noFronterDuration = Duration.Zero;
            
            // Sum up all switch durations for each member
            // switches with multiple members will result in the duration to add up to more than the actual period range
            foreach (var sw in await GetTruncatedSwitchList(system, periodStart, periodEnd))
            {
                foreach (var member in sw.Members)
                {
                    if (!dict.ContainsKey(member)) dict.Add(member, sw.TimespanWithinRange);
                    else dict[member] += sw.TimespanWithinRange;
                }

                if (sw.Members.Count == 0) noFronterDuration += sw.TimespanWithinRange;
            }
            
            return new PerMemberSwitchDuration
            {
                MemberSwitchDurations = dict,
                NoFronterDuration = noFronterDuration
            };
        }
    }
}