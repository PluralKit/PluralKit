using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using NodaTime;

using Serilog;

namespace PluralKit.Core {
    public class PostgresDataStore: IDataStore {
        private IDatabase _conn;
        private ILogger _logger;

        public PostgresDataStore(IDatabase conn, ILogger logger)
        {
            _conn = conn;
            _logger = logger;
        }

        public async Task<IEnumerable<PKMember>> GetConflictingProxies(PKSystem system, ProxyTag tag)
        {
            using (var conn = await _conn.Obtain())
                // return await conn.QueryAsync<PKMember>("select * from (select *, (unnest(proxy_tags)).prefix as prefix, (unnest(proxy_tags)).suffix as suffix from members where system = @System) as _ where prefix ilike @Prefix and suffix ilike @Suffix", new
                // {
                //     System = system.Id,
                //     Prefix = tag.Prefix.Replace("%", "\\%") + "%",
                //     Suffix = "%" + tag.Suffix.Replace("%", "\\%")
                // });
                return await conn.QueryAsync<PKMember>("select * from (select *, (unnest(proxy_tags)).prefix as prefix, (unnest(proxy_tags)).suffix as suffix from members where system = @System) as _ where prefix = @Prefix and suffix = @Suffix", new
                {
                    System = system.Id,
                    Prefix = tag.Prefix,
                    Suffix = tag.Suffix
                });
        }
        
        public async Task<PKSystem> CreateSystem(string systemName = null) {
            PKSystem system;
            using (var conn = await _conn.Obtain())
                system = await conn.QuerySingleAsync<PKSystem>("insert into systems (hid, name) values (find_free_system_hid(), @Name) returning *", new { Name = systemName });

            _logger.Information("Created system {System}", system.Id);
            // New system has no accounts, therefore nothing gets cached, therefore no need to invalidate caches right here
            return system;
        }

        public async Task AddAccount(PKSystem system, ulong accountId) {
            // We have "on conflict do nothing" since linking an account when it's already linked to the same system is idempotent
            // This is used in import/export, although the pk;link command checks for this case beforehand
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("insert into accounts (uid, system) values (@Id, @SystemId) on conflict do nothing", new { Id = accountId, SystemId = system.Id });

            _logger.Information("Linked system {System} to account {Account}", system.Id, accountId);
        }

        public async Task RemoveAccount(PKSystem system, ulong accountId) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from accounts where uid = @Id and system = @SystemId", new { Id = accountId, SystemId = system.Id });

            _logger.Information("Unlinked system {System} from account {Account}", system.Id, accountId);
        }

        public async Task<PKSystem> GetSystemByAccount(ulong accountId) {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleOrDefaultAsync<PKSystem>("select systems.* from systems, accounts where accounts.system = systems.id and accounts.uid = @Id", new { Id = accountId });
        }

        public async Task<PKSystem> GetSystemByHid(string hid) {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleOrDefaultAsync<PKSystem>("select * from systems where systems.hid = @Hid", new { Hid = hid.ToLower() });
        }

        public async Task<PKSystem> GetSystemByToken(string token) {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleOrDefaultAsync<PKSystem>("select * from systems where token = @Token", new { Token = token });
        }

        public async Task<PKSystem> GetSystemById(int id)
        {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleOrDefaultAsync<PKSystem>("select * from systems where id = @Id", new { Id = id });
        }

        public async Task SaveSystem(PKSystem system) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("update systems set name = @Name, description = @Description, tag_suffix = @TagSuffix, tag_prefix = @TagPrefix, avatar_url = @AvatarUrl, token = @Token, ui_tz = @UiTz, description_privacy = @DescriptionPrivacy, member_list_privacy = @MemberListPrivacy, front_privacy = @FrontPrivacy, front_history_privacy = @FrontHistoryPrivacy, pings_enabled = @PingsEnabled where id = @Id", system);

            _logger.Information("Updated system {@System}", system);
        }

        public async Task DeleteSystem(PKSystem system)
        {
            using var conn = await _conn.Obtain();
            
            // Fetch the list of accounts *before* deletion so we can cache-bust all of those
            var accounts = (await conn.QueryAsync<ulong>("select uid from accounts where system = @Id", system)).ToArray();
            await conn.ExecuteAsync("delete from systems where id = @Id", system);
            
            _logger.Information("Deleted system {System}", system.Id);
        }

        public async Task<IEnumerable<ulong>> GetSystemAccounts(PKSystem system)
        {
            using (var conn = await _conn.Obtain())
                return await conn.QueryAsync<ulong>("select uid from accounts where system = @Id", new { Id = system.Id });
        }

        public async Task DeleteAllSwitches(PKSystem system)
        {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from switches where system = @Id", system);
        }

        public async Task<PKMember> CreateMember(SystemId system, string name) {
            PKMember member;
            using (var conn = await _conn.Obtain())
                member = await conn.QuerySingleAsync<PKMember>("insert into members (hid, system, name) values (find_free_member_hid(), @SystemId, @Name) returning *", new {
                    SystemID = system,
                    Name = name
                });

            _logger.Information("Created member {Member}", member.Id);
            return member;
        }

        public async Task<PKMember> GetMemberById(MemberId id) {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleOrDefaultAsync<PKMember>("select * from members where id = @Id", new { Id = id });
        }
        
        public async Task<PKMember> GetMemberByHid(string hid) {
            using (var conn = await _conn.Obtain())
                return await conn.QuerySingleOrDefaultAsync<PKMember>("select * from members where hid = @Hid", new { Hid = hid.ToLower() });
        }

        public async Task<PKMember> GetMemberByName(PKSystem system, string name) {
            // QueryFirst, since members can (in rare cases) share names
            using (var conn = await _conn.Obtain())
                return await conn.QueryFirstOrDefaultAsync<PKMember>("select * from members where lower(name) = lower(@Name) and system = @SystemID", new { Name = name, SystemID = system.Id });
        }

        public async Task<PKMember> GetMemberByDisplayName(PKSystem system, string name) {
            // QueryFirst, since members can (in rare cases) share display names
            using (var conn = await _conn.Obtain())
                return await conn.QueryFirstOrDefaultAsync<PKMember>("select * from members where lower(display_name) = lower(@Name) and system = @SystemID", new { Name = name, SystemID = system.Id });
        }

        public IAsyncEnumerable<PKMember> GetSystemMembers(PKSystem system, bool orderByName)
        {
            var sql = "select * from members where system = @SystemID";
            if (orderByName) sql += " order by lower(name) asc";
            return _conn.QueryStreamAsync<PKMember>(sql, new { SystemID = system.Id });
        }

        public async Task SaveMember(PKMember member) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("update members set name = @Name, display_name = @DisplayName, description = @Description, color = @Color, avatar_url = @AvatarUrl, birthday = @Birthday, pronouns = @Pronouns, proxy_tags = @ProxyTags, keep_proxy = @KeepProxy, member_visibility = @MemberVisibility, description_privacy = @DescriptionPrivacy,  name_privacy = @NamePrivacy, avatar_privacy = @AvatarPrivacy, birthday_privacy = @BirthdayPrivacy, pronoun_privacy = @PronounPrivacy, metadata_privacy = @MetadataPrivacy where id = @Id", member);

            _logger.Information("Updated member {@Member}", member);
        }

        public async Task DeleteMember(PKMember member) {
            using (var conn = await _conn.Obtain())
                await conn.ExecuteAsync("delete from members where id = @Id", member);

            _logger.Information("Deleted member {@Member}", member);
        }

        public async Task<int> GetSystemMemberCount(SystemId id, bool includePrivate)
        {
            var query = "select count(*) from members where system = @Id";
            if (!includePrivate) query += " and member_visibility = 1"; // 1 = public
            
            using (var conn = await _conn.Obtain())
                return await conn.ExecuteScalarAsync<int>(query, new { id });
        }

        public async Task<ulong> GetTotalMembers()
        {
            using (var conn = await _conn.Obtain())
                return await conn.ExecuteScalarAsync<ulong>("select count(id) from members");
        }
        
        public async Task AddMessage(IPKConnection conn, ulong senderId, ulong guildId, ulong channelId, ulong postedMessageId, ulong triggerMessageId, MemberId proxiedMemberId) {
            // "on conflict do nothing" in the (pretty rare) case of duplicate events coming in from Discord, which would lead to a DB error before
            await conn.ExecuteAsync("insert into messages(mid, guild, channel, member, sender, original_mid) values(@MessageId, @GuildId, @ChannelId, @MemberId, @SenderId, @OriginalMid) on conflict do nothing", new {
                MessageId = postedMessageId,
                GuildId = guildId,
                ChannelId = channelId,
                MemberId = proxiedMemberId,
                SenderId = senderId,
                OriginalMid = triggerMessageId
            });

            _logger.Debug("Stored message {Message} in channel {Channel}", postedMessageId, channelId);
        }

        public async Task<FullMessage> GetMessage(ulong id)
        {
            using (var conn = await _conn.Obtain())
                return (await conn.QueryAsync<PKMessage, PKMember, PKSystem, FullMessage>("select messages.*, members.*, systems.* from messages, members, systems where (mid = @Id or original_mid = @Id) and messages.member = members.id and systems.id = members.system", (msg, member, system) => new FullMessage
                {
                    Message = msg,
                    System = system,
                    Member = member
                }, new { Id = id })).FirstOrDefault();
        }

        public async Task DeleteMessage(ulong id) {
            using (var conn = await _conn.Obtain())
                if (await conn.ExecuteAsync("delete from messages where mid = @Id", new { Id = id }) > 0)
                    _logger.Information("Deleted message {Message}", id);
        }

        public async Task DeleteMessagesBulk(IReadOnlyCollection<ulong> ids)
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

        public async Task AddSwitch(SystemId system, IEnumerable<PKMember> members)
        {
            // Use a transaction here since we're doing multiple executed commands in one
            await using var conn = await _conn.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            
            // First, we insert the switch itself
            var sw = await conn.QuerySingleAsync<PKSwitch>("insert into switches(system) values (@System) returning *",
                new {System = system});

            // Then we insert each member in the switch in the switch_members table
            // TODO: can we parallelize this or send it in bulk somehow?
            foreach (var member in members)
            {
                await conn.ExecuteAsync(
                    "insert into switch_members(switch, member) values(@Switch, @Member)",
                    new {Switch = sw.Id, Member = member.Id});
            }

            // Finally we commit the tx, since the using block will otherwise rollback it
            await tx.CommitAsync();

            _logger.Information("Registered switch {Switch} in system {System} with members {@Members}", sw.Id, system, members.Select(m => m.Id));
        }

        public IAsyncEnumerable<PKSwitch> GetSwitches(SystemId system)
        {
            // TODO: refactor the PKSwitch data structure to somehow include a hydrated member list
            // (maybe when we get caching in?)
            return _conn.QueryStreamAsync<PKSwitch>(
                "select * from switches where system = @System order by timestamp desc",
                new {System = system});
        }

        public async Task<int> GetSwitchCount(PKSystem system)
        {
            using var conn = await _conn.Obtain();
            return await conn.QuerySingleAsync<int>("select count(*) from switches where system = @Id", system);
        }

        public async IAsyncEnumerable<SwitchMembersListEntry> GetSwitchMembersList(PKSystem system, Instant start, Instant end)
        {
            // Wrap multiple commands in a single transaction for performance
            await using var conn = await _conn.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            
            // Find the time of the last switch outside the range as it overlaps the range
            // If no prior switch exists, the lower bound of the range remains the start time
            var lastSwitch = await conn.QuerySingleOrDefaultAsync<Instant>(
                @"SELECT COALESCE(MAX(timestamp), @Start)
                        FROM switches
                        WHERE switches.system = @System
                        AND switches.timestamp < @Start",
                new { System = system.Id, Start = start });

            // Then collect the time and members of all switches that overlap the range
            var switchMembersEntries = conn.QueryStreamAsync<SwitchMembersListEntry>(
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

            // Yield each value here
            await foreach (var entry in switchMembersEntries)
                yield return entry;
            
            // Don't really need to worry about the transaction here, we're not doing any *writes*
        }

        public IAsyncEnumerable<PKMember> GetSwitchMembers(PKSwitch sw)
        {
            return _conn.QueryStreamAsync<PKMember>(
                "select * from switch_members, members where switch_members.member = members.id and switch_members.switch = @Switch order by switch_members.id",
                new {Switch = sw.Id});
        }

        public async Task<PKSwitch> GetLatestSwitch(SystemId system) => 
            await GetSwitches(system).FirstOrDefaultAsync();

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

        public async Task<IEnumerable<SwitchListEntry>> GetPeriodFronters(PKSystem system, Instant periodStart, Instant periodEnd)
        {
            // TODO: IAsyncEnumerable-ify this one
            
            // Returns the timestamps and member IDs of switches overlapping the range, in chronological (newest first) order
            var switchMembers = await GetSwitchMembersList(system, periodStart, periodEnd).ToListAsync();

            // query DB for all members involved in any of the switches above and collect into a dictionary for future use
            // this makes sure the return list has the same instances of PKMember throughout, which is important for the dictionary
            // key used in GetPerMemberSwitchDuration below
            Dictionary<MemberId, PKMember> memberObjects;
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
                    Members = g.Where(x => x.Member != default(MemberId)).Select(x => memberObjects[x.Member]).ToList()
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
        public async Task<FrontBreakdown> GetFrontBreakdown(PKSystem system, Instant periodStart, Instant periodEnd)
        {
            var dict = new Dictionary<PKMember, Duration>();

            var noFronterDuration = Duration.Zero;

            // Sum up all switch durations for each member
            // switches with multiple members will result in the duration to add up to more than the actual period range

            var actualStart = periodEnd; // will be "pulled" down
            var actualEnd = periodStart; // will be "pulled" up

            foreach (var sw in await GetPeriodFronters(system, periodStart, periodEnd))
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

            return new FrontBreakdown
            {
                MemberSwitchDurations = dict,
                NoFronterDuration = noFronterDuration,
                RangeStart = actualStart,
                RangeEnd = actualEnd
            };
        }
    }
}