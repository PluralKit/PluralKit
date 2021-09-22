using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using Newtonsoft.Json.Linq;

using NodaTime;

using NpgsqlTypes;

namespace PluralKit.Core
{
    public partial class BulkImporter
    {
        private async Task<ImportResultNew> ImportPluralKit(JObject importFile)
        {
            var patch = SystemPatch.FromJSON(importFile);

            try
            {
                patch.AssertIsValid();
            }
            catch (ValidationError e)
            {
                throw new ImportException($"Field {e.Message} in export file is invalid.");
            }

            await _repo.UpdateSystem(_conn, _system.Id, patch, _tx);

            var members = importFile.Value<JArray>("members");
            var groups = importFile.Value<JArray>("groups");
            var switches = importFile.Value<JArray>("switches");

            var newMembers = members.Count(m =>
            {
                var (found, _) = TryGetExistingMember(m.Value<string>("id"), m.Value<string>("name"));
                return found == null;
            });
            await AssertMemberLimitNotReached(newMembers);

            if (groups != null)
            {
                var newGroups = groups.Count(g =>
                {
                    var (found, _) = TryGetExistingGroup(g.Value<string>("id"), g.Value<string>("name"));
                    return found == null;
                });
                await AssertGroupLimitNotReached(newGroups);
            }

            foreach (JObject member in members)
                await ImportMember(member);

            if (groups != null)
                foreach (JObject group in groups)
                    await ImportGroup(group);

            if (switches.Any(sw => sw.Value<JArray>("members").Any(m => !_knownMemberIdentifiers.ContainsKey((string)m))))
                throw new ImportException("One or more switches include members that haven't been imported.");

            await ImportSwitches(switches);

            return _result;
        }

        private async Task ImportMember(JObject member)
        {
            var id = member.Value<string>("id");
            var name = member.Value<string>("name");

            var (found, isHidExisting) = TryGetExistingMember(id, name);
            var isNewMember = found == null;
            var referenceName = isHidExisting ? id : name;

            if (isNewMember)
                _result.Added++;
            else
                _result.Modified++;

            _logger.Debug(
                "Importing member with identifier {FileId} to system {System} (is creating new member? {IsCreatingNewMember})",
                referenceName, _system.Id, isNewMember
            );

            var patch = MemberPatch.FromJSON(member);
            try
            {
                patch.AssertIsValid();
            }
            catch (FieldTooLongError e)
            {
                throw new ImportException($"Field {e.Name} in member {referenceName} is too long ({e.ActualLength} > {e.MaxLength}).");
            }
            catch (ValidationError e)
            {
                throw new ImportException($"Field {e.Message} in member {referenceName} is invalid.");
            }

            MemberId? memberId = found;

            if (isNewMember)
            {
                var newMember = await _repo.CreateMember(_conn, _system.Id, patch.Name.Value, _tx);
                memberId = newMember.Id;
            }

            _knownMemberIdentifiers[id] = memberId.Value;

            await _repo.UpdateMember(_conn, memberId.Value, patch, _tx);
        }

        private async Task ImportGroup(JObject group)
        {
            var id = group.Value<string>("id");
            var name = group.Value<string>("name");

            var (found, isHidExisting) = TryGetExistingGroup(id, name);
            var isNewGroup = found == null;
            var referenceName = isHidExisting ? id : name;

            _logger.Debug(
                "Importing group with identifier {FileId} to system {System} (is creating new group? {IsCreatingNewGroup})",
                referenceName, _system.Id, isNewGroup
            );

            var patch = GroupPatch.FromJson(group);
            try
            {
                patch.AssertIsValid();
            }
            catch (FieldTooLongError e)
            {
                throw new ImportException($"Field {e.Name} in group {referenceName} is too long ({e.ActualLength} > {e.MaxLength}).");
            }
            catch (ValidationError e)
            {
                throw new ImportException($"Field {e.Message} in group {referenceName} is invalid.");
            }

            GroupId? groupId = found;

            if (isNewGroup)
            {
                var newGroup = await _repo.CreateGroup(_conn, _system.Id, patch.Name.Value, _tx);
                groupId = newGroup.Id;
            }

            _knownGroupIdentifiers[id] = groupId.Value;

            await _repo.UpdateGroup(_conn, groupId.Value, patch, _tx);

            var groupMembers = group.Value<JArray>("members");
            var currentGroupMembers = (await _conn.QueryAsync<MemberId>(
                "select member_id from group_members where group_id = @groupId",
                new { groupId = groupId.Value }
            )).ToList();

            await using (var importer = _conn.BeginBinaryImport("copy group_members (group_id, member_id) from stdin (format binary)"))
            {
                foreach (var memberIdentifier in groupMembers)
                {
                    if (!_knownMemberIdentifiers.TryGetValue(memberIdentifier.ToString(), out var memberId))
                        throw new Exception($"Attempted to import group member with member identifier {memberIdentifier} but could not find a recently imported member with this id!");

                    if (currentGroupMembers.Contains(memberId))
                        continue;

                    await importer.StartRowAsync();
                    await importer.WriteAsync(groupId.Value.Value, NpgsqlDbType.Integer);
                    await importer.WriteAsync(memberId.Value, NpgsqlDbType.Integer);
                }

                await importer.CompleteAsync();
            }

        }
        private async Task ImportSwitches(JArray switches)
        {
            var existingSwitches = (await _conn.QueryAsync<PKSwitch>("select * from switches where system = @System", new { System = _system.Id })).ToList();
            var existingTimestamps = existingSwitches.Select(sw => sw.Timestamp).ToImmutableHashSet();
            var lastSwitchId = existingSwitches.Count != 0 ? existingSwitches.Select(sw => sw.Id).Max() : (SwitchId?)null;

            if (switches.Count > 10000)
                throw new ImportException($"Too many switches present in import file.");

            // Import switch definitions
            var importedSwitches = new Dictionary<Instant, JArray>();
            await using (var importer = _conn.BeginBinaryImport("copy switches (system, timestamp) from stdin (format binary)"))
            {
                foreach (var sw in switches)
                {
                    var timestampString = sw.Value<string>("timestamp");
                    var timestamp = DateTimeFormats.TimestampExportFormat.Parse(timestampString);
                    if (!timestamp.Success) throw new ImportException($"Switch timestamp {timestampString} is not an valid timestamp.");

                    // Don't import duplicate switches
                    if (existingTimestamps.Contains(timestamp.Value)) continue;

                    // Otherwise, write to importer
                    await importer.StartRowAsync();
                    await importer.WriteAsync(_system.Id.Value, NpgsqlDbType.Integer);
                    await importer.WriteAsync(timestamp.Value, NpgsqlDbType.Timestamp);

                    var members = sw.Value<JArray>("members");
                    if (members.Count > Limits.MaxSwitchMemberCount)
                        throw new ImportException($"Switch with timestamp {timestampString} contains too many members ({members.Count} > 100).");

                    // Note that we've imported a switch with this timestamp
                    importedSwitches[timestamp.Value] = sw.Value<JArray>("members");
                }

                // Commit the import
                await importer.CompleteAsync();
            }

            // Now, fetch all the switches we just added (so, now we get their IDs too)
            // IDs are sequential, so any ID in this system, with a switch ID > the last max, will be one we just added
            var justAddedSwitches = await _conn.QueryAsync<PKSwitch>(
                "select * from switches where system = @System and id > @LastSwitchId",
                new { System = _system.Id, LastSwitchId = lastSwitchId?.Value ?? -1 });

            // Lastly, import the switch members
            await using (var importer = _conn.BeginBinaryImport("copy switch_members (switch, member) from stdin (format binary)"))
            {
                foreach (var justAddedSwitch in justAddedSwitches)
                {
                    if (!importedSwitches.TryGetValue(justAddedSwitch.Timestamp, out var switchMembers))
                        throw new Exception($"Found 'just-added' switch (by ID) with timestamp {justAddedSwitch.Timestamp}, but this did not correspond to a timestamp we just added a switch entry of! :/");

                    // We still assume timestamps are unique and non-duplicate, so:
                    foreach (var memberIdentifier in switchMembers)
                    {
                        if (!_knownMemberIdentifiers.TryGetValue((string)memberIdentifier, out var memberId))
                            throw new Exception($"Attempted to import switch with member identifier {memberIdentifier} but could not find an entry in the id map for this! :/");

                        await importer.StartRowAsync();
                        await importer.WriteAsync(justAddedSwitch.Id.Value, NpgsqlDbType.Integer);
                        await importer.WriteAsync(memberId.Value, NpgsqlDbType.Integer);
                    }
                }

                await importer.CompleteAsync();
            }
        }
    }
}