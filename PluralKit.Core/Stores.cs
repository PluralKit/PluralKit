using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Metrics.Logging;
using Dapper;
using NodaTime;
using Npgsql;
using PluralKit.Core;

using Serilog;

namespace PluralKit {
    public class SystemStore {
        private DbConnectionFactory _conn;
        private ILogger _logger;

        public SystemStore(DbConnectionFactory conn, ILogger logger)
        {
            this._conn = conn;
            _logger = logger.ForContext<SystemStore>();
        }

        public async Task<PKSystem> Create(string systemName = null) {
            string hid;
            do
            {
                hid = Utils.GenerateHid();
            } while (await GetByHid(hid) != null);

            PKSystem system;
            using (var conn = await _conn.Obtain())
                system = await conn.QuerySingleAsync<PKSystem>("insert into systems (hid, name) values (@Hid, @Name) returning *", new { Hid = hid, Name = systemName });

            _logger.Information("Created system {System}", system.Id);
            return system;
        }

        public async Task Link(PKSystem system, ulong accountId) {
            // We have "on conflict do nothing" since linking an account when it's already linked to the same system is idempotent
            // This is used in import/export, although the pk;link command checks for this case beforehand
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("insert into accounts (uid, system) values (@Id, @SystemId) on conflict do nothing", new { Id = accountId, SystemId = system.Id });

            _logger.Information("Linked system {System} to account {Account}", system.Id, accountId);
        }

        public async Task Unlink(PKSystem system, ulong accountId) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from accounts where uid = @Id and system = @SystemId", new { Id = accountId, SystemId = system.Id });

            _logger.Information("Unlinked system {System} from account {Account}", system.Id, accountId);
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

            _logger.Information("Updated system {@System}", system);
        }

        public async Task Delete(PKSystem system) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from systems where id = @Id", system);
            _logger.Information("Deleted system {System}", system.Id);
        }

        public async Task<IEnumerable<ulong>> GetLinkedAccountIds(PKSystem system)
        {
            using (var conn = await _conn.Obtain())
                return await conn.QueryAsync<ulong>("select uid from accounts where system = @Id", new { Id = system.Id });
        }

        public async Task<ulong> Count()
        {
            using (var conn = await _conn.Obtain())
                return await conn.ExecuteScalarAsync<ulong>("select count(id) from systems");
        }
    }

    public class MemberStore {
        private DbConnectionFactory _conn;
        private ILogger _logger;

        public MemberStore(DbConnectionFactory conn, ILogger logger)
        {
            this._conn = conn;
            _logger = logger.ForContext<MemberStore>();
        }

        public async Task<PKMember> Create(PKSystem system, string name) {
            string hid;
            do
            {
                hid = Utils.GenerateHid();
            } while (await GetByHid(hid) != null);

            PKMember member;
            using (var conn = await _conn.Obtain())
                member = await conn.QuerySingleAsync<PKMember>("insert into members (hid, system, name) values (@Hid, @SystemId, @Name) returning *", new {
                    Hid = hid,
                    SystemID = system.Id,
                    Name = name
                });

            _logger.Information("Created member {Member}", member.Id);
            return member;
        }

        public async Task<Dictionary<string,PKMember>> CreateMultiple(PKSystem system, Dictionary<string,string> names)
        {
            using (var conn = await _conn.Obtain())
            using (var tx = conn.BeginTransaction())
            {
                var results = new Dictionary<string, PKMember>();
                foreach (var name in names)
                {
                    string hid;
                    do
                    {
                        hid = await conn.QuerySingleOrDefaultAsync<string>("SELECT @Hid WHERE NOT EXISTS (SELECT id FROM members WHERE hid = @Hid LIMIT 1)", new
                        {
                            Hid = Utils.GenerateHid()
                        });
                    } while (hid == null);
                    var member = await conn.QuerySingleAsync<PKMember>("INSERT INTO members (hid, system, name) VALUES (@Hid, @SystemId, @Name) RETURNING *", new
                    {
                        Hid = hid,
                        SystemID = system.Id,
                        Name = name.Value
                    });
                    results.Add(name.Key, member);
                }

                tx.Commit();
                _logger.Information("Created {MemberCount} members for system {SystemID}", names.Count(), system.Hid);
                return results;
            }
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
                    return proxiedName.Length > Limits.MaxProxyNameLength || proxiedName.Length < 2;
                }).ToList();
        }

        public async Task<IEnumerable<PKMember>> GetBySystem(PKSystem system) {
            using (var conn = await _conn.Obtain())
                return await conn.QueryAsync<PKMember>("select * from members where system = @SystemID", new { SystemID = system.Id });
        }

        public async Task Save(PKMember member) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("update members set name = @Name, display_name = @DisplayName, description = @Description, color = @Color, avatar_url = @AvatarUrl, birthday = @Birthday, pronouns = @Pronouns, prefix = @Prefix, suffix = @Suffix where id = @Id", member);

            _logger.Information("Updated member {@Member}", member);
        }

        public async Task Delete(PKMember member) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from members where id = @Id", member);

            _logger.Information("Deleted member {@Member}", member);
        }

        public async Task<int> MessageCount(PKMember member)
        {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleAsync<int>("select count(*) from messages where member = @Id", member);
        }

        public struct MessageBreakdownListEntry
        {
            public int Member;
            public int MessageCount;
        }

        public async Task<IEnumerable<MessageBreakdownListEntry>> MessageCountsPerMember(PKSystem system)
        {
            using (var conn = await _conn.Obtain())
                return await conn.QueryAsync<MessageBreakdownListEntry>(
                    @"SELECT messages.member, COUNT(messages.member) messagecount
                        FROM members
                        JOIN messages
                        ON members.id = messages.member
                        WHERE members.system = @System
                        GROUP BY messages.member",
                    new { System = system.Id });
        }

        public async Task<int> MemberCount(PKSystem system)
        {
            using (var conn = await _conn.Obtain())
                return await conn.ExecuteScalarAsync<int>("select count(*) from members where system = @Id", system);
        }

        public async Task<ulong> Count()
        {
            using (var conn = await _conn.Obtain())
                return await conn.ExecuteScalarAsync<ulong>("select count(id) from members");
        }
    }

    public class MessageStore {
        public struct PKMessage
        {
            public ulong Mid;
            public ulong Channel;
            public ulong Sender;
            public ulong? OriginalMid;
        }
        public class StoredMessage
        {
            public PKMessage Message;
            public PKMember Member;
            public PKSystem System;
        }

        private DbConnectionFactory _conn;
        private ILogger _logger;

        public MessageStore(DbConnectionFactory conn, ILogger logger)
        {
            this._conn = conn;
            _logger = logger.ForContext<MessageStore>();
        }

        public async Task Store(ulong senderId, ulong messageId, ulong channelId, ulong originalMessage, PKMember member) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("insert into messages(mid, channel, member, sender, original_mid) values(@MessageId, @ChannelId, @MemberId, @SenderId, @OriginalMid)", new {
                    MessageId = messageId,
                    ChannelId = channelId,
                    MemberId = member.Id,
                    SenderId = senderId,
                    OriginalMid = originalMessage
                });

            _logger.Information("Stored message {Message} in channel {Channel}", messageId, channelId);
        }

        public async Task<StoredMessage> Get(ulong id)
        {
            using (var conn = await _conn.Obtain())
                return (await conn.QueryAsync<PKMessage, PKMember, PKSystem, StoredMessage>("select messages.*, members.*, systems.* from messages, members, systems where (mid = @Id or original_mid = @Id) and messages.member = members.id and systems.id = members.system", (msg, member, system) => new StoredMessage
                {
                    Message = msg,
                    System = system,
                    Member = member
                }, new { Id = id })).FirstOrDefault();
        }

        public async Task Delete(ulong id) {
            using (var conn = await _conn.Obtain())
                if (await conn.ExecuteAsync("delete from messages where mid = @Id", new { Id = id }) > 0)
                    _logger.Information("Deleted message {Message}", id);
        }

        public async Task BulkDelete(IReadOnlyCollection<ulong> ids)
        {
            using (var conn = await _conn.Obtain())
            {
                // Npgsql doesn't support ulongs in general - we hacked around it for plain ulongs but tbh not worth it for collections of ulong
                // Hence we map them to single longs, which *are* supported (this is ok since they're Technically (tm) stored as signed longs in the db anyway)
                var foundCount = await conn.ExecuteAsync("delete from messages where mid = any(@Ids)", new {Ids = ids.Select(id => (long) id).ToArray()});
                if (foundCount > 0)
                    _logger.Information("Bulk deleted messages {Messages}, {FoundCount} found", ids, foundCount);
            }
        }

        public async Task<ulong> Count()
        {
            using (var conn = await _conn.Obtain())
                return await conn.ExecuteScalarAsync<ulong>("select count(mid) from messages");
        }
    }

    public class SwitchStore
    {
        private DbConnectionFactory _conn;
        private ILogger _logger;

        public SwitchStore(DbConnectionFactory conn, ILogger logger)
        {
            _conn = conn;
            _logger = logger.ForContext<SwitchStore>();
        }

        public async Task RegisterSwitch(PKSystem system, ICollection<PKMember> members)
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

                _logger.Information("Registered switch {Switch} in system {System} with members {@Members}", sw.Id, system.Id, members.Select(m => m.Id));
            }
        }

        public async Task BulkImportSwitches(PKSystem system, ICollection<Tuple<Instant, ICollection<PKMember>>> switches)
        {
            // Read existing switches to enforce unique timestamps
            var priorSwitches = await GetSwitches(system);
            var lastSwitchId = priorSwitches.Any()
                ? priorSwitches.Max(x => x.Id)
                : 0;
            
            using (var conn = (PerformanceTrackingConnection) await _conn.Obtain())
            {
                using (var tx = conn.BeginTransaction())
                {
                    // Import switches in bulk
                    using (var importer = conn.BeginBinaryImport("COPY switches (system, timestamp) FROM STDIN (FORMAT BINARY)"))
                    {
                        foreach (var sw in switches)
                        {
                            // If there's already a switch at this time, move on
                            if (priorSwitches.Any(x => x.Timestamp.Equals(sw.Item1)))
                                continue;

                            // Otherwise, add it to the importer
                            importer.StartRow();
                            importer.Write(system.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                            importer.Write(sw.Item1, NpgsqlTypes.NpgsqlDbType.Timestamp);
                        }
                        importer.Complete(); // Commits the copy operation so dispose won't roll it back
                    }

                    // Get all switches that were created above and don't have members for ID lookup
                    var switchesWithoutMembers =
                        await conn.QueryAsync<PKSwitch>(@"
                        SELECT switches.*
                        FROM switches
                        LEFT JOIN switch_members
                        ON switch_members.switch = switches.id
                        WHERE switches.id > @LastSwitchId
                        AND switches.system = @System
                        AND switch_members.id IS NULL", new { LastSwitchId = lastSwitchId, System = system.Id });

                    // Import switch_members in bulk
                    using (var importer = conn.BeginBinaryImport("COPY switch_members (switch, member) FROM STDIN (FORMAT BINARY)"))
                    {
                        // Iterate over the switches we created above and set their members
                        foreach (var pkSwitch in switchesWithoutMembers)
                        {
                            // If this isn't in our import set, move on
                            var sw = switches.FirstOrDefault(x => x.Item1.Equals(pkSwitch.Timestamp));
                            if (sw == null)
                                continue;

                            // Loop through associated members to add each to the switch
                            foreach (var m in sw.Item2)
                            {
                                // Skip switch-outs - these don't have switch_members
                                if (m == null)
                                    continue;
                                importer.StartRow();
                                importer.Write(pkSwitch.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                                importer.Write(m.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                            }
                        }
                        importer.Complete(); // Commits the copy operation so dispose won't roll it back
                    }
                    tx.Commit();
                }
            }

            _logger.Information("Completed bulk import of switches for system {0}", system.Hid);
        }
        
        public async Task RegisterSwitches(PKSystem system, ICollection<Tuple<Instant, ICollection<PKMember>>> switches)
        {
            // Use a transaction here since we're doing multiple executed commands in one
            using (var conn = await _conn.Obtain())
            using (var tx = conn.BeginTransaction())
            {
                foreach (var s in switches)
                {
                    // First, we insert the switch itself
                    var sw = await conn.QueryFirstOrDefaultAsync<PKSwitch>(
                            @"insert into switches(system, timestamp)
                            select @System, @Timestamp
                            where not exists (
                                select * from switches
                                where system = @System and timestamp::timestamp(0) = @Timestamp
                                limit 1
                            )
                            returning *",
                        new { System = system.Id, Timestamp = s.Item1 });

                    // If we inserted a switch, also insert each member in the switch in the switch_members table
                    if (sw != null && s.Item2.Any())
                        await conn.ExecuteAsync(
                            "insert into switch_members(switch, member) select @Switch, * FROM unnest(@Members)",
                            new { Switch = sw.Id, Members = s.Item2.Select(x => x.Id).ToArray() });
                }

                // Finally we commit the tx, since the using block will otherwise rollback it
                tx.Commit();

                _logger.Information("Registered {SwitchCount} switches in system {System}", switches.Count, system.Id);
            }
        }

        public async Task<IEnumerable<PKSwitch>> GetSwitches(PKSystem system, int count = 9999999)
        {
            // TODO: refactor the PKSwitch data structure to somehow include a hydrated member list
            // (maybe when we get caching in?)
            using (var conn = await _conn.Obtain())
                return await conn.QueryAsync<PKSwitch>("select * from switches where system = @System order by timestamp desc limit @Count", new {System = system.Id, Count = count});
        }

        public struct SwitchMembersListEntry
        {
            public int Member;
            public Instant Timestamp;
        }

        public async Task<IEnumerable<SwitchMembersListEntry>> GetSwitchMembersList(PKSystem system, Instant start, Instant end)
        {
            // Wrap multiple commands in a single transaction for performance
            using (var conn = await _conn.Obtain())
            using (var tx = conn.BeginTransaction())
            {
                // Find the time of the last switch outside the range as it overlaps the range
                // If no prior switch exists, the lower bound of the range remains the start time
                var lastSwitch = await conn.QuerySingleOrDefaultAsync<Instant>(
                    @"SELECT COALESCE(MAX(timestamp), @Start)
                        FROM switches
                        WHERE switches.system = @System
                        AND switches.timestamp < @Start",
                    new { System = system.Id, Start = start });

                // Then collect the time and members of all switches that overlap the range
                var switchMembersEntries = await conn.QueryAsync<SwitchMembersListEntry>(
                    @"SELECT switch_members.member, switches.timestamp
                        FROM switches
                        LEFT JOIN switch_members
                        ON switches.id = switch_members.switch
                        WHERE switches.system = @System
                        AND (
	                        switches.timestamp >= @Start
	                        OR switches.timestamp = @LastSwitch
                        )
                        AND switches.timestamp < @End
                        ORDER BY switches.timestamp DESC",
                    new { System = system.Id, Start = start, End = end, LastSwitch = lastSwitch });

                // Commit and return the list
                tx.Commit();
                return switchMembersEntries;
            }
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

            _logger.Information("Moved switch {Switch} to {Time}", sw.Id, time);
        }

        public async Task DeleteSwitch(PKSwitch sw)
        {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from switches where id = @Id", new {Id = sw.Id});

            _logger.Information("Deleted switch {Switch}");
        }

        public async Task<ulong> Count()
        {
            using (var conn = await _conn.Obtain())
                return await conn.ExecuteScalarAsync<ulong>("select count(id) from switches");
        }

        public struct SwitchListEntry
        {
            public ICollection<PKMember> Members;
            public Instant TimespanStart;
            public Instant TimespanEnd;
        }

        public async Task<IEnumerable<SwitchListEntry>> GetTruncatedSwitchList(PKSystem system, Instant periodStart, Instant periodEnd)
        {
            // Returns the timestamps and member IDs of switches overlapping the range, in chronological (newest first) order
            var switchMembers = await GetSwitchMembersList(system, periodStart, periodEnd);

            // query DB for all members involved in any of the switches above and collect into a dictionary for future use
            // this makes sure the return list has the same instances of PKMember throughout, which is important for the dictionary
            // key used in GetPerMemberSwitchDuration below
            Dictionary<int, PKMember> memberObjects;
            using (var conn = await _conn.Obtain())
            {
                memberObjects = (
                    await conn.QueryAsync<PKMember>(
                        "select * from members where id = any(@Switches)", // lol postgres specific `= any()` syntax
                        new { Switches = switchMembers.Select(m => m.Member).Distinct().ToList() })
                    ).ToDictionary(m => m.Id);
            }

            // Initialize entries - still need to loop to determine the TimespanEnd below
            var entries =
                from item in switchMembers
                group item by item.Timestamp into g
                select new SwitchListEntry
                {
                    TimespanStart = g.Key,
                    Members = g.Where(x => x.Member != 0).Select(x => memberObjects[x.Member]).ToList()
                };

            // Loop through every switch that overlaps the range and add it to the output list
            // end time is the *FOLLOWING* switch's timestamp - we cheat by working backwards from the range end, so no dates need to be compared
            var endTime = periodEnd;
            var outList = new List<SwitchListEntry>();
            foreach (var e in entries)
            {
                // Override the start time of the switch if it's outside the range (only true for the "out of range" switch we included above)
                var switchStartClamped = e.TimespanStart < periodStart
                    ? periodStart
                    : e.TimespanStart;

                outList.Add(new SwitchListEntry
                {
                    Members = e.Members,
                    TimespanStart = switchStartClamped,
                    TimespanEnd = endTime
                });

                // next switch's end is this switch's start (we're working backward in time)
                endTime = e.TimespanStart;
            }

            return outList;
        }

        public struct PerMemberSwitchDuration
        {
            public Dictionary<PKMember, Duration> MemberSwitchDurations;
            public Duration NoFronterDuration;
            public Instant RangeStart;
            public Instant RangeEnd;
        }

        public async Task<PerMemberSwitchDuration> GetPerMemberSwitchDuration(PKSystem system, Instant periodStart,
            Instant periodEnd)
        {
            var dict = new Dictionary<PKMember, Duration>();

            var noFronterDuration = Duration.Zero;

            // Sum up all switch durations for each member
            // switches with multiple members will result in the duration to add up to more than the actual period range

            var actualStart = periodEnd; // will be "pulled" down
            var actualEnd = periodStart; // will be "pulled" up

            foreach (var sw in await GetTruncatedSwitchList(system, periodStart, periodEnd))
            {
                var span = sw.TimespanEnd - sw.TimespanStart;
                foreach (var member in sw.Members)
                {
                    if (!dict.ContainsKey(member)) dict.Add(member, span);
                    else dict[member] += span;
                }

                if (sw.Members.Count == 0) noFronterDuration += span;

                if (sw.TimespanStart < actualStart) actualStart = sw.TimespanStart;
                if (sw.TimespanEnd > actualEnd) actualEnd = sw.TimespanEnd;
            }

            return new PerMemberSwitchDuration
            {
                MemberSwitchDurations = dict,
                NoFronterDuration = noFronterDuration,
                RangeStart = actualStart,
                RangeEnd = actualEnd
            };
        }
    }
}