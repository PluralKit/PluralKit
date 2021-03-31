#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using NodaTime;

using NpgsqlTypes;

namespace PluralKit.Core
{
    public class BulkImporter: IAsyncDisposable
    {
        private readonly SystemId _systemId;
        private readonly IPKConnection _conn;
        private readonly IPKTransaction _tx;
        private readonly Dictionary<string, MemberId> _knownMembers = new Dictionary<string, MemberId>();
        private readonly Dictionary<string, PKMember> _existingMembersByHid = new Dictionary<string, PKMember>();
        private readonly Dictionary<string, PKMember> _existingMembersByName = new Dictionary<string, PKMember>();

        private BulkImporter(SystemId systemId, IPKConnection conn, IPKTransaction tx)
        {
            _systemId = systemId;
            _conn = conn;
            _tx = tx;
        }

        public static async Task<BulkImporter> Begin(PKSystem system, IPKConnection conn)
        {
            var tx = await conn.BeginTransactionAsync();
            var importer = new BulkImporter(system.Id, conn, tx);
            await importer.Begin();
            return importer;
        }

        public async Task Begin()
        {
            // Fetch all members in the system and log their names and hids
            var members = await _conn.QueryAsync<PKMember>("select id, hid, name from members where system = @System",
                new {System = _systemId});
            foreach (var m in members)
            {
                _existingMembersByHid[m.Hid] = m;
                _existingMembersByName[m.Name] = m;
            }
        }
        
        /// <summary>
        /// Checks whether trying to add a member with the given hid and name would result in creating a new member (as opposed to just updating one).
        /// </summary>
        public bool IsNewMember(string hid, string name) => FindExistingMemberInSystem(hid, name) == null;

        /// <summary>
        /// Imports a member into the database
        /// </summary>
        /// <remarks>If an existing member exists in this system that matches this member in either HID or name, it'll overlay the member information on top of this instead.</remarks>
        /// <param name="identifier">An opaque identifier string that refers to this member regardless of source. Is used when importing switches. Value is irrelevant, but should be consistent with the same member later.</param>
        /// <param name="potentialHid">When trying to match the member to an existing member, will use a member with this HID if present in system.</param>
        /// <param name="potentialName">When trying to match the member to an existing member, will use a member with this name if present in system.</param>
        /// <param name="patch">A member patch struct containing the data to apply to this member </param>
        /// <returns>The inserted member object, which may or may not share an ID or HID with the input member.</returns>
        public async Task<PKMember> AddMember(string identifier, string potentialHid, string potentialName, MemberPatch patch)
        {
            // See if we can find a member that matches this one
            // if not, roll a new hid and we'll insert one with that
            // (we can't trust the hid given in the member, it might let us overwrite another system's members)
            var existingMember = FindExistingMemberInSystem(potentialHid, potentialName); 
            string newHid = existingMember?.Hid ?? await _conn.QuerySingleAsync<string>("find_free_member_hid", commandType: CommandType.StoredProcedure);

            // Upsert member data and return the ID
            QueryBuilder qb = QueryBuilder.Upsert("members", "hid")
                .Constant("hid", "@Hid")
                .Constant("system", "@System");

            if (patch.Name.IsPresent) qb.Variable("name", "@Name");
            if (patch.DisplayName.IsPresent) qb.Variable("display_name", "@DisplayName");
            if (patch.Description.IsPresent) qb.Variable("description", "@Description");
            if (patch.Color.IsPresent) qb.Variable("color", "@Color");
            if (patch.AvatarUrl.IsPresent) qb.Variable("avatar_url", "@AvatarUrl");
            if (patch.ProxyTags.IsPresent) qb.Variable("proxy_tags", "@ProxyTags");
            if (patch.Birthday.IsPresent) qb.Variable("birthday", "@Birthday");
            if (patch.KeepProxy.IsPresent) qb.Variable("keep_proxy", "@KeepProxy");

            var newMember = await _conn.QueryFirstAsync<PKMember>(qb.Build("returning *"),
                new
                {
                    Hid = newHid,
                    System = _systemId,
                    Name = patch.Name.Value,
                    DisplayName = patch.DisplayName.Value,
                    Description = patch.Description.Value,
                    Color = patch.Color.Value,
                    AvatarUrl = patch.AvatarUrl.Value,
                    KeepProxy = patch.KeepProxy.Value,
                    ProxyTags = patch.ProxyTags.Value,
                    Birthday = patch.Birthday.Value
                });

            // Log this member ID by the given identifier
            _knownMembers[identifier] = newMember.Id;
            return newMember;
        }

        private PKMember? FindExistingMemberInSystem(string hid, string name)
        {
            if (_existingMembersByHid.TryGetValue(hid, out var byHid)) return byHid;
            if (_existingMembersByName.TryGetValue(name, out var byName)) return byName;
            return null;
        }

        /// <summary>
        /// Register switches in bulk.
        /// </summary>
        /// <remarks>This function assumes there are no duplicate switches (ie. switches with the same timestamp).</remarks>
        public async Task AddSwitches(IReadOnlyCollection<SwitchInfo> switches)
        {
            // Ensure we're aware of all the members we're trying to import from
            if (!switches.All(sw => sw.MemberIdentifiers.All(m => _knownMembers.ContainsKey(m))))
                throw new ArgumentException("One or more switch members haven't been added using this importer");
            
            // Fetch the existing switches in the database so we can avoid duplicates
            var existingSwitches = (await _conn.QueryAsync<PKSwitch>("select * from switches where system = @System", new {System = _systemId})).ToList();
            var existingTimestamps = existingSwitches.Select(sw => sw.Timestamp).ToImmutableHashSet();
            var lastSwitchId = existingSwitches.Count != 0 ? existingSwitches.Select(sw => sw.Id).Max() : (SwitchId?) null;

            // Import switch definitions
            var importedSwitches = new Dictionary<Instant, SwitchInfo>();
            await using (var importer = _conn.BeginBinaryImport("copy switches (system, timestamp) from stdin (format binary)"))
            {
                foreach (var sw in switches)
                {
                    // Don't import duplicate switches
                    if (existingTimestamps.Contains(sw.Timestamp)) continue;
                    
                    // Otherwise, write to importer
                    await importer.StartRowAsync();
                    await importer.WriteAsync(_systemId.Value, NpgsqlDbType.Integer);
                    await importer.WriteAsync(sw.Timestamp, NpgsqlDbType.Timestamp);
                    
                    // Note that we've imported a switch with this timestamp
                    importedSwitches[sw.Timestamp] = sw;
                }

                // Commit the import
                await importer.CompleteAsync();
            }
            
            // Now, fetch all the switches we just added (so, now we get their IDs too)
            // IDs are sequential, so any ID in this system, with a switch ID > the last max, will be one we just added
            var justAddedSwitches = await _conn.QueryAsync<PKSwitch>(
                "select * from switches where system = @System and id > @LastSwitchId",
                new {System = _systemId, LastSwitchId = lastSwitchId?.Value ?? -1});
            
            // Lastly, import the switch members
            await using (var importer = _conn.BeginBinaryImport("copy switch_members (switch, member) from stdin (format binary)"))
            {
                foreach (var justAddedSwitch in justAddedSwitches)
                {
                    if (!importedSwitches.TryGetValue(justAddedSwitch.Timestamp, out var switchInfo))
                        throw new Exception($"Found 'just-added' switch (by ID) with timestamp {justAddedSwitch.Timestamp}, but this did not correspond to a timestamp we just added a switch entry of! :/");
                    
                    // We still assume timestamps are unique and non-duplicate, so:
                    var members = switchInfo.MemberIdentifiers;
                    foreach (var memberIdentifier in members)
                    {
                        if (!_knownMembers.TryGetValue(memberIdentifier, out var memberId))
                            throw new Exception($"Attempted to import switch with member identifier {memberIdentifier} but could not find an entry in the id map for this! :/");
                        
                        await importer.StartRowAsync();
                        await importer.WriteAsync(justAddedSwitch.Id.Value, NpgsqlDbType.Integer);
                        await importer.WriteAsync(memberId.Value, NpgsqlDbType.Integer);
                    }
                }

                await importer.CompleteAsync();
            }
        }

        public struct SwitchInfo
        {
            public Instant Timestamp;
            
            /// <summary>
            /// An ordered list of "member identifiers" matching with the identifier parameter passed to <see cref="BulkImporter.AddMember"/>.
            /// </summary>
            public IReadOnlyList<string> MemberIdentifiers;
        }

        public async ValueTask DisposeAsync() => 
            await _tx.CommitAsync();
    }
}