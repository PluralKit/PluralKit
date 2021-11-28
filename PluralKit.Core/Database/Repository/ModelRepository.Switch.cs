using Dapper;

using Newtonsoft.Json.Linq;

using NodaTime;

using NpgsqlTypes;

using SqlKata;

namespace PluralKit.Core;

// todo: move the rest of the queries in here to SqlKata, if possible
public partial class ModelRepository
{
    public async Task<PKSwitch> AddSwitch(IPKConnection conn, SystemId system,
                                          IReadOnlyCollection<MemberId> members)
    {
        // Use a transaction here since we're doing multiple executed commands in one
        await using var tx = await conn.BeginTransactionAsync();

        // First, we insert the switch itself
        var sw = await conn.QuerySingleAsync<PKSwitch>("insert into switches(system) values (@System) returning *",
            new { System = system });

        // Then we insert each member in the switch in the switch_members table
        await using (var w =
                     conn.BeginBinaryImport("copy switch_members (switch, member) from stdin (format binary)"))
        {
            foreach (var member in members)
            {
                await w.StartRowAsync();
                await w.WriteAsync(sw.Id.Value, NpgsqlDbType.Integer);
                await w.WriteAsync(member.Value, NpgsqlDbType.Integer);
            }

            await w.CompleteAsync();
        }

        // Finally we commit the tx, since the using block will otherwise rollback it
        await tx.CommitAsync();

        _logger.Information("Created {SwitchId} in {SystemId}: {Members}", sw.Id, system, members);
        _ = _dispatch.Dispatch(sw.Id, new UpdateDispatchData
        {
            Event = DispatchEvent.CREATE_SWITCH,
            EventData = JObject.FromObject(new
            {
                id = sw.Uuid.ToString(),
                timestamp = sw.Timestamp.FormatExport(),
                members = await GetMemberGuids(members),
            }),
        });
        return sw;
    }

    public async Task EditSwitch(IPKConnection conn, SwitchId switchId, IReadOnlyCollection<MemberId> members)
    {
        // Use a transaction here since we're doing multiple executed commands in one
        await using var tx = await conn.BeginTransactionAsync();

        // Remove the old members from the switch
        await conn.ExecuteAsync("delete from switch_members where switch = @Switch",
            new { Switch = switchId });

        // Add the new members
        await using (var w =
                     conn.BeginBinaryImport("copy switch_members (switch, member) from stdin (format binary)"))
        {
            foreach (var member in members)
            {
                await w.StartRowAsync();
                await w.WriteAsync(switchId.Value, NpgsqlDbType.Integer);
                await w.WriteAsync(member.Value, NpgsqlDbType.Integer);
            }

            await w.CompleteAsync();
        }

        // Finally we commit the tx, since the using block will otherwise rollback it
        await tx.CommitAsync();

        _ = _dispatch.Dispatch(switchId, new UpdateDispatchData
        {
            Event = DispatchEvent.UPDATE_SWITCH,
            EventData = JObject.FromObject(new
            {
                members = await GetMemberGuids(members),
            }),
        });

        _logger.Information("Updated {SwitchId} members: {Members}", switchId, members);
    }

    public async Task MoveSwitch(SwitchId id, Instant time)
    {
        _logger.Information("Updated {SwitchId} timestamp: {SwitchTimestamp}", id, time);
        var query = new Query("switches").AsUpdate(new { timestamp = time }).Where("id", id);
        await _db.ExecuteQuery(query);
        _ = _dispatch.Dispatch(id, new UpdateDispatchData
        {
            Event = DispatchEvent.UPDATE_SWITCH,
            EventData = JObject.FromObject(new
            {
                timestamp = time.FormatExport(),
            }),
        });
    }

    public async Task DeleteSwitch(SwitchId id)
    {
        var existingSwitch = await GetSwitch(id);

        var query = new Query("switches").AsDelete().Where("id", id);
        await _db.ExecuteQuery(query);
        _logger.Information("Deleted {Switch}", id);
        _ = _dispatch.Dispatch(existingSwitch.System, existingSwitch.Uuid, DispatchEvent.DELETE_SWITCH);
    }

    public async Task DeleteAllSwitches(SystemId system)
    {
        _logger.Information("Deleted all switches in {SystemId}", system);
        var query = new Query("switches").AsDelete().Where("system", system);
        await _db.ExecuteQuery(query);
        _ = _dispatch.Dispatch(system, new UpdateDispatchData
        {
            Event = DispatchEvent.DELETE_ALL_SWITCHES
        });
    }

    public IAsyncEnumerable<PKSwitch> GetSwitches(SystemId system)
    {
        // TODO: refactor the PKSwitch data structure to somehow include a hydrated member list
        var query = new Query("switches").Where("system", system).OrderByDesc("timestamp");
        return _db.QueryStream<PKSwitch>(query);
    }

    public Task<PKSwitch?> GetSwitch(SwitchId id)
        => _db.QueryFirst<PKSwitch?>(new Query("switches").Where("id", id));

    public Task<PKSwitch> GetSwitchByUuid(Guid uuid)
    {
        var query = new Query("switches").Where("uuid", uuid);
        return _db.QueryFirst<PKSwitch>(query);
    }

    public Task<int> GetSwitchCount(SystemId system)
    {
        var query = new Query("switches").SelectRaw("count(*)").Where("system", system);
        return _db.QueryFirst<int>(query);
    }

    public async IAsyncEnumerable<SwitchMembersListEntry> GetSwitchMembersList(IPKConnection conn,
                                                                               SystemId system, Instant start,
                                                                               Instant end)
    {
        // Wrap multiple commands in a single transaction for performance
        await using var tx = await conn.BeginTransactionAsync();

        // Find the time of the last switch outside the range as it overlaps the range
        // If no prior switch exists, the lower bound of the range remains the start time
        var lastSwitch = await conn.QuerySingleOrDefaultAsync<Instant>(
            @"SELECT COALESCE(MAX(timestamp), @Start)
                FROM switches
                WHERE switches.system = @System
                AND switches.timestamp < @Start",
            new { System = system, Start = start });

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
            new { System = system, Start = start, End = end, LastSwitch = lastSwitch });

        // Yield each value here
        await foreach (var entry in switchMembersEntries)
            yield return entry;

        // Don't really need to worry about the transaction here, we're not doing any *writes*
    }

    public IAsyncEnumerable<PKMember> GetSwitchMembers(IPKConnection conn, SwitchId sw) =>
        conn.QueryStreamAsync<PKMember>(
            "select * from switch_members, members where switch_members.member = members.id and switch_members.switch = @Switch order by switch_members.id",
            new { Switch = sw });

    public Task<PKSwitch> GetLatestSwitch(SystemId system)
    {
        var query = new Query("switches").Where("system", system).OrderByDesc("timestamp").Limit(1);
        return _db.QueryFirst<PKSwitch>(query);
    }

    public async Task<IEnumerable<SwitchListEntry>> GetPeriodFronters(IPKConnection conn,
                    SystemId system, GroupId? group, Instant periodStart, Instant periodEnd)
    {
        // TODO: IAsyncEnumerable-ify this one
        // TODO: this doesn't belong in the repo

        // Returns the timestamps and member IDs of switches overlapping the range, in chronological (newest first) order
        var switchMembers = await GetSwitchMembersList(conn, system, periodStart, periodEnd).ToListAsync();

        // query DB for all members involved in any of the switches above and collect into a dictionary for future use
        // this makes sure the return list has the same instances of PKMember throughout, which is important for the dictionary
        // key used in GetPerMemberSwitchDuration below
        var membersList = await conn.QueryAsync<PKMember>(
            "select * from members where id = any(@Switches)", // lol postgres specific `= any()` syntax
            new { Switches = switchMembers.Select(m => m.Member.Value).Distinct().ToList() });
        var memberObjects = membersList.ToDictionary(m => m.Id);

        // check if a group ID is provided. if so, query DB for all members of said group, otherwise use membersList
        var groupMembersList = group != null
            ? await conn.QueryAsync<PKMember>(
                "select * from members inner join group_members on members.id = group_members.member_id where group_id = @id",
                new { id = group })
            : membersList;
        var groupMemberObjects = groupMembersList.ToDictionary(m => m.Id);

        // Initialize entries - still need to loop to determine the TimespanEnd below
        // use groupMemberObjects to make sure no members outside of the specified group (if present) are selected
        var entries =
            from item in switchMembers
            group item by item.Timestamp
            into g
            select new SwitchListEntry
            {
                TimespanStart = g.Key,
                Members = g.Where(x => x.Member != default && groupMemberObjects.Any(m => x.Member == m.Key))
                    .Select(x => memberObjects[x.Member])
                    .ToList()
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

    public async Task<FrontBreakdown> GetFrontBreakdown(IPKConnection conn, SystemId system, GroupId? group,
                                                        Instant periodStart,
                                                        Instant periodEnd)
    {
        // TODO: this doesn't belong in the repo
        var dict = new Dictionary<PKMember, Duration>();

        var noFronterDuration = Duration.Zero;

        // Sum up all switch durations for each member
        // switches with multiple members will result in the duration to add up to more than the actual period range

        var actualStart = periodEnd; // will be "pulled" down
        var actualEnd = periodStart; // will be "pulled" up

        foreach (var sw in await GetPeriodFronters(conn, system, group, periodStart, periodEnd))
        {
            var span = sw.TimespanEnd - sw.TimespanStart;
            foreach (var member in sw.Members)
                if (!dict.ContainsKey(member)) dict.Add(member, span);
                else dict[member] += span;

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

public struct SwitchListEntry
{
    public ICollection<PKMember> Members;
    public Instant TimespanStart;
    public Instant TimespanEnd;
}

public struct FrontBreakdown
{
    public Dictionary<PKMember, Duration> MemberSwitchDurations;
    public Duration NoFronterDuration;
    public Instant RangeStart;
    public Instant RangeEnd;
}

public struct SwitchMembersListEntry
{
    public MemberId Member;
    public Instant Timestamp;
}