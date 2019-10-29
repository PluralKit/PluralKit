using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Text;
using PluralKit.Core;
using Serilog;

namespace PluralKit.Bot
{
    public class DataFileService
    {
        private IDataStore _data;
        private ILogger _logger;

        public DataFileService(ILogger logger, IDataStore data)
        {
            _data = data;
            _logger = logger.ForContext<DataFileService>();
        }

        public async Task<DataFileSystem> ExportSystem(PKSystem system)
        {
            // Export members
            var members = new List<DataFileMember>();
            var pkMembers = await _data.GetSystemMembers(system); // Read all members in the system
            var messageCounts = await _data.GetMemberMessageCountBulk(system); // Count messages proxied by all members in the system
            members.AddRange(pkMembers.Select(m => new DataFileMember
            {
                Id = m.Hid,
                Name = m.Name,
                DisplayName = m.DisplayName,
                Description = m.Description,
                Birthday = m.Birthday != null ? Formats.DateExportFormat.Format(m.Birthday.Value) : null,
                Pronouns = m.Pronouns,
                Color = m.Color,
                AvatarUrl = m.AvatarUrl,
                Prefix = m.Prefix,
                Suffix = m.Suffix,
                Created = Formats.TimestampExportFormat.Format(m.Created),
                MessageCount = messageCounts.Where(x => x.Member == m.Id).Select(x => x.MessageCount).FirstOrDefault()
            }));

            // Export switches
            var switches = new List<DataFileSwitch>();
            var switchList = await _data.GetPeriodFronters(system, Instant.FromDateTimeUtc(DateTime.MinValue.ToUniversalTime()), SystemClock.Instance.GetCurrentInstant());
            switches.AddRange(switchList.Select(x => new DataFileSwitch
            {
                Timestamp = Formats.TimestampExportFormat.Format(x.TimespanStart),
                Members = x.Members.Select(m => m.Hid).ToList() // Look up member's HID using the member export from above
            }));

            return new DataFileSystem
            {
                Id = system.Hid,
                Name = system.Name,
                Description = system.Description,
                Tag = system.Tag,
                AvatarUrl = system.AvatarUrl,
                TimeZone = system.UiTz,
                Members = members,
                Switches = switches,
                Created = Formats.TimestampExportFormat.Format(system.Created),
                LinkedAccounts = (await _data.GetSystemAccounts(system)).ToList()
            };
        }

        public async Task<ImportResult> ImportSystem(DataFileSystem data, PKSystem system, ulong accountId)
        {
            // TODO: make atomic, somehow - we'd need to obtain one IDbConnection and reuse it
            // which probably means refactoring SystemStore.Save and friends etc
            var result = new ImportResult {
                AddedNames = new List<string>(),
                ModifiedNames = new List<string>(),
                Success = true // Assume success unless indicated otherwise
            };
            var dataFileToMemberMapping = new Dictionary<string, PKMember>();
            var unmappedMembers = new List<DataFileMember>();

            // If we don't already have a system to save to, create one
            if (system == null)
                system = await _data.CreateSystem(data.Name);
            result.System = system;

            // Apply system info
            system.Name = data.Name;
            if (data.Description != null) system.Description = data.Description;
            if (data.Tag != null) system.Tag = data.Tag;
            if (data.AvatarUrl != null) system.AvatarUrl = data.AvatarUrl;
            if (data.TimeZone != null) system.UiTz = data.TimeZone ?? "UTC";
            await _data.SaveSystem(system);

            // Make sure to link the sender account, too
            await _data.AddAccount(system, accountId);

            // Determine which members already exist and which ones need to be created
            var existingMembers = await _data.GetSystemMembers(system);
            foreach (var d in data.Members)
            {
                // Try to look up the member with the given ID
                var match = existingMembers.FirstOrDefault(m => m.Hid.Equals(d.Id));
                if (match == null)
                    match = existingMembers.FirstOrDefault(m => m.Name.Equals(d.Name)); // Try with the name instead
                if (match != null)
                {
                    dataFileToMemberMapping.Add(d.Id, match); // Relate the data file ID to the PKMember for importing switches
                    result.ModifiedNames.Add(d.Name);
                }
                else
                {
                    unmappedMembers.Add(d); // Track members that weren't found so we can create them all
                    result.AddedNames.Add(d.Name);
                }
            }

            // If creating the unmatched members would put us over the member limit, abort before creating any members
            // new total: # in the system + (# in the file - # in the file that already exist)
            if (data.Members.Count - dataFileToMemberMapping.Count + existingMembers.Count() > Limits.MaxMemberCount)
            {
                result.Success = false;
                result.Message = $"Import would exceed the maximum number of members ({Limits.MaxMemberCount}).";
                result.AddedNames.Clear();
                result.ModifiedNames.Clear();
                return result;
            }

            // Create all unmapped members in one transaction
            // These consist of members from another PluralKit system or another framework (e.g. Tupperbox)
            var membersToCreate = new Dictionary<string, string>();
            unmappedMembers.ForEach(x => membersToCreate.Add(x.Id, x.Name));
            var newMembers = await _data.CreateMembersBulk(system, membersToCreate);
            foreach (var member in newMembers)
                dataFileToMemberMapping.Add(member.Key, member.Value);

            // Update members with data file properties
            // TODO: parallelize?
            foreach (var dataMember in data.Members)
            {
                dataFileToMemberMapping.TryGetValue(dataMember.Id, out PKMember member);
                if (member == null)
                    continue;

                // Apply member info
                member.Name = dataMember.Name;
                if (dataMember.DisplayName != null) member.DisplayName = dataMember.DisplayName;
                if (dataMember.Description != null) member.Description = dataMember.Description;
                if (dataMember.Color != null) member.Color = dataMember.Color;
                if (dataMember.AvatarUrl != null) member.AvatarUrl = dataMember.AvatarUrl;
                if (dataMember.Prefix != null || dataMember.Suffix != null)
                {
                    member.Prefix = dataMember.Prefix;
                    member.Suffix = dataMember.Suffix;
                }

                if (dataMember.Birthday != null)
                {
                    var birthdayParse = Formats.DateExportFormat.Parse(dataMember.Birthday);
                    member.Birthday = birthdayParse.Success ? (LocalDate?)birthdayParse.Value : null;
                }

                await _data.SaveMember(member);
            }

            // Re-map the switch members in the likely case IDs have changed
            var mappedSwitches = new List<ImportedSwitch>();
            foreach (var sw in data.Switches)
            {
                var timestamp = InstantPattern.ExtendedIso.Parse(sw.Timestamp).Value;
                var swMembers = new List<PKMember>();
                swMembers.AddRange(sw.Members.Select(x =>
                    dataFileToMemberMapping.FirstOrDefault(y => y.Key.Equals(x)).Value));
                mappedSwitches.Add(new ImportedSwitch
                {
                    Timestamp = timestamp,
                    Members = swMembers
                });
            }
            // Import switches
            if (mappedSwitches.Any())
                await _data.AddSwitchesBulk(system, mappedSwitches);

            _logger.Information("Imported system {System}", system.Hid);
            return result;
        }
    }

    public struct ImportResult
    {
        public ICollection<string> AddedNames;
        public ICollection<string> ModifiedNames;
        public PKSystem System;
        public bool Success;
        public string Message;
    }

    public struct DataFileSystem
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("description")] public string Description;
        [JsonProperty("tag")] public string Tag;
        [JsonProperty("avatar_url")] public string AvatarUrl;
        [JsonProperty("timezone")] public string TimeZone;
        [JsonProperty("members")] public ICollection<DataFileMember> Members;
        [JsonProperty("switches")] public ICollection<DataFileSwitch> Switches;
        [JsonProperty("accounts")] public ICollection<ulong> LinkedAccounts;
        [JsonProperty("created")] public string Created;

        private bool TimeZoneValid => TimeZone == null || DateTimeZoneProviders.Tzdb.GetZoneOrNull(TimeZone) != null;

        [JsonIgnore] public bool Valid => TimeZoneValid && Members != null && Members.All(m => m.Valid);
    }

    public struct DataFileMember
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("display_name")] public string DisplayName;
        [JsonProperty("description")] public string Description;
        [JsonProperty("birthday")] public string Birthday;
        [JsonProperty("pronouns")] public string Pronouns;
        [JsonProperty("color")] public string Color;
        [JsonProperty("avatar_url")] public string AvatarUrl;
        [JsonProperty("prefix")] public string Prefix;
        [JsonProperty("suffix")] public string Suffix;
        [JsonProperty("message_count")] public int MessageCount;
        [JsonProperty("created")] public string Created;

        [JsonIgnore] public bool Valid => Name != null;
    }

    public struct DataFileSwitch
    {
        [JsonProperty("timestamp")] public string Timestamp;
        [JsonProperty("members")] public ICollection<string> Members;
    }

    public struct TupperboxConversionResult
    {
        public bool HadGroups;
        public bool HadIndividualTags;
        public bool HadMultibrackets;
        public DataFileSystem System;
    }

    public struct TupperboxProfile
    {
        [JsonProperty("tuppers")] public ICollection<TupperboxTupper> Tuppers;
        [JsonProperty("groups")] public ICollection<TupperboxGroup> Groups;

        [JsonIgnore] public bool Valid => Tuppers != null && Groups != null && Tuppers.All(t => t.Valid) && Groups.All(g => g.Valid);

        public TupperboxConversionResult ToPluralKit()
        {
            // Set by member conversion function
            string lastSetTag = null;
            
            TupperboxConversionResult output = default(TupperboxConversionResult);
            
            output.System = new DataFileSystem
            {
                Members = Tuppers.Select(t => t.ToPluralKit(ref lastSetTag, ref output.HadMultibrackets,
                    ref output.HadGroups, ref output.HadMultibrackets)).ToList(),
                Switches = new List<DataFileSwitch>(),
                // If we haven't had multiple tags set, use the last (and only) one we set as the system tag
                Tag = !output.HadIndividualTags ? lastSetTag : null
            };
            return output;
        }
    }

    public struct TupperboxTupper
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("avatar_url")] public string AvatarUrl;
        [JsonProperty("brackets")] public ICollection<string> Brackets;
        [JsonProperty("posts")] public int Posts; // Not supported by PK
        [JsonProperty("show_brackets")] public bool ShowBrackets; // Not supported by PK
        [JsonProperty("birthday")] public string Birthday;
        [JsonProperty("description")] public string Description;
        [JsonProperty("tag")] public string Tag; // Not supported by PK
        [JsonProperty("group_id")] public string GroupId; // Not supported by PK
        [JsonProperty("group_pos")] public int? GroupPos; // Not supported by PK

        [JsonIgnore] public bool Valid => Name != null && Brackets != null && Brackets.Count % 2 == 0;

        public DataFileMember ToPluralKit(ref string lastSetTag, ref bool multipleTags, ref bool hasGroup, ref bool hasMultiBrackets)
        {
            // If we've set a tag before and it's not the same as this one,
            // then we have multiple unique tags and we pass that flag back to the caller
            if (Tag != null && lastSetTag != null && lastSetTag != Tag) multipleTags = true;
            lastSetTag = Tag;

            // If this member is in a group, we have a (used) group and we flag that
            if (GroupId != null) hasGroup = true;

            // Brackets in Tupperbox format are arranged as a single array
            // [prefix1, suffix1, prefix2, suffix2, prefix3... etc]
            // If there are more than two entries this member has multiple brackets and we flag that
            if (Brackets.Count > 2) hasMultiBrackets = true;

            return new DataFileMember
            {
                Id = Guid.NewGuid().ToString(), // Note: this is only ever used for lookup purposes
                Name = Name,
                AvatarUrl = AvatarUrl,
                Birthday = Birthday,
                Description = Description,
                Prefix = Brackets.FirstOrDefault().NullIfEmpty(),
                Suffix = Brackets.Skip(1).FirstOrDefault().NullIfEmpty() // TODO: can Tupperbox members have no proxies at all?
            };
        }
    }

    public struct TupperboxGroup
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("description")] public string Description;
        [JsonProperty("tag")] public string Tag;

        [JsonIgnore] public bool Valid => true;
    }
}