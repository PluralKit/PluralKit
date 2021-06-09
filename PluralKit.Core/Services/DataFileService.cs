using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;

using NodaTime;

using Serilog;

namespace PluralKit.Core
{
    public class DataFileService
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly ILogger _logger;

        public DataFileService(ILogger logger, IDatabase db, ModelRepository repo)
        {
            _db = db;
            _repo = repo;
            _logger = logger.ForContext<DataFileService>();
        }

        public async Task<DataFileSystem> ExportSystem(PKSystem system)
        {
            await using var conn = await _db.Obtain();
            
            // Export members
            var members =  await _repo.GetSystemMembers(conn, system.Id).ToListAsync(); // Read all members in the system

            // Export groups
            var groups = (await conn.QueryGroupList(system.Id)).ToList();
            
            // Export switches
            var switches = new List<DataFileSwitch>();
            var switchList = await _repo.GetPeriodFronters(conn, system.Id, null, Instant.FromDateTimeUtc(DateTime.MinValue.ToUniversalTime()), SystemClock.Instance.GetCurrentInstant());
            switches.AddRange(switchList.Select(x => new DataFileSwitch
            {
                Timestamp = x.TimespanStart.FormatExport(),
                Members = x.Members.Select(m => m.Hid).ToList() // Look up member's HID using the member export from above
            }));

            return new DataFileSystem
            {
                Version = 1,
                Id = system.Hid,
                Name = system.Name,
                Description = system.Description,
                Tag = system.Tag,
                AvatarUrl = system.AvatarUrl,
                TimeZone = system.UiTz,
                Members = members,
                Groups = groups,
                Switches = switches,
                Created = system.Created.FormatExport(),
                LinkedAccounts = (await _repo.GetSystemAccounts(conn, system.Id)).ToList()
            };
        }

        public async Task<ImportResult> ImportSystem(DataFileSystem data, PKSystem system, ulong accountId)
        {
            await using var conn = await _db.Obtain();
            
            var result = new ImportResult {
                AddedNames = new List<string>(),
                ModifiedNames = new List<string>(),
                AddedGroupNames = new List<string>(),
                ModifiedGroupNames = new List<string>(),
                System = system,
                Success = true // Assume success unless indicated otherwise
            };
            
            // If we don't already have a system to save to, create one
            if (system == null)
            {
                system = result.System = await _repo.CreateSystem(conn, data.Name);
                await _repo.AddAccount(conn, system.Id, accountId);
            }

            var memberLimit = system.MemberLimitOverride ?? Limits.MaxMemberCount;

            // Apply system info
            var patch = new SystemPatch {Name = data.Name};
            if (data.Description != null) patch.Description = data.Description;
            if (data.Tag != null) patch.Tag = data.Tag;
            if (data.AvatarUrl != null) patch.AvatarUrl = data.AvatarUrl;
            if (data.TimeZone != null) patch.UiTz = data.TimeZone ?? "UTC";
            await _repo.UpdateSystem(conn, system.Id, patch);
            
            // -- Member/switch import --
            await using (var imp = await BulkImporter.Begin(system, conn))
            {
                // Tally up the members that didn't exist before, and check member count on import
                // If creating the unmatched members would put us over the member limit, abort before creating any members
                var memberCountBefore = await _repo.GetSystemMemberCount(conn, system.Id);
                var membersToAdd = data.Members.Count(m => imp.IsNewMember(m.Hid, m.Name));
                if (memberCountBefore + membersToAdd > memberLimit)
                {
                    result.Success = false;
                    result.Message = $"Import would exceed the maximum number of members ({memberLimit}).";
                    return result;
                }
                
                async Task DoImportMember(BulkImporter imp, PKMember fileMember)
                {
                    var isCreatingNewMember = imp.IsNewMember(fileMember.Hid, fileMember.Name);

                    // Use the file member's id as the "unique identifier" for the importing (actual value is irrelevant but needs to be consistent)
                    _logger.Debug(
                        "Importing member with identifier {FileId} to system {System} (is creating new member? {IsCreatingNewMember})",
                        fileMember.Id, system.Id, isCreatingNewMember);
                    var newMember = await imp.AddMember(fileMember.Hid, fileMember.Hid, fileMember.Name, fileMember.ToMemberPatch());

                    if (isCreatingNewMember)
                        result.AddedNames.Add(newMember.Name);
                    else
                        result.ModifiedNames.Add(newMember.Name);
                }

                async Task DoImportGroup(BulkImporter imp, PKGroup group)
                {
                    var isCreatingNewGroup = imp.IsNewGroup(group.Hid, group.Name);
                    
                    _logger.Debug("Importing group with identifier {id} to system {System} (is creating new group? {IsCreatingNewGroup})",
                        group.Hid, system.Id, isCreatingNewGroup);
                    var newGroup = await imp.AddGroup(group.Hid, group.Hid, group.Name, group.ToGroupPatch());

                    if (isCreatingNewGroup)
                        result.AddedGroupNames.Add(group.Name);
                    else
                        result.ModifiedGroupNames.Add(group.Name);
                }
                
                // Can't parallelize this because we can't reuse the same connection/tx inside the importer
                foreach (var m in data.Members) 
                    await DoImportMember(imp, m);

                foreach (var g in data.Groups)
                    await DoImportGroup(imp, g);
                
                // Lastly, import the switches
                await imp.AddSwitches(data.Switches.Select(sw => new BulkImporter.SwitchInfo
                {
                    Timestamp = DateTimeFormats.TimestampExportFormat.Parse(sw.Timestamp).Value,
                    // "Members" here is from whatever ID the data file uses, which the bulk importer can map to the real IDs! :)
                    MemberIdentifiers = sw.Members.ToList()
                }).ToList());
            }

            _logger.Information("Imported system {System}", system.Hid);
            return result; 
        }
    }

    public struct ImportResult
    {
        // todo: should these just be ints?
        public ICollection<string> AddedNames;
        public ICollection<string> ModifiedNames;
        public ICollection<string> AddedGroupNames;
        public ICollection<string> ModifiedGroupNames;
        public PKSystem System;
        public bool Success;
        public string Message;
    }

    public struct DataFileSystem
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("description")] public string Description;
        [JsonProperty("tag")] public string Tag;
        [JsonProperty("avatar_url")] public string AvatarUrl;
        [JsonProperty("timezone")] public string TimeZone;
        [JsonProperty("members")] public ICollection<PKMember> Members;
        [JsonProperty("groups")] public ICollection<ListedGroup> Groups;
        [JsonProperty("switches")] public ICollection<DataFileSwitch> Switches;
        [JsonProperty("accounts")] public ICollection<ulong> LinkedAccounts;
        [JsonProperty("created")] public string Created;

        private bool TimeZoneValid => TimeZone == null || DateTimeZoneProviders.Tzdb.GetZoneOrNull(TimeZone) != null;

        [JsonIgnore] public bool Valid =>
            TimeZoneValid &&
            Members != null &&
            // no need to check this here, it is checked later as part of the import
            // Members.Count <= Limits.MaxMemberCount &&
            Members.All(m => m.Valid) &&
            Switches != null &&
            Switches.Count < 10000 &&
            Switches.All(s => s.Valid) &&
            !Name.IsLongerThan(Limits.MaxSystemNameLength) &&
            !Description.IsLongerThan(Limits.MaxDescriptionLength) &&
            !Tag.IsLongerThan(Limits.MaxSystemTagLength) &&
            !AvatarUrl.IsLongerThan(1000);
    }
    public struct DataFileSwitch
    {
        [JsonProperty("timestamp")] public string Timestamp;
        [JsonProperty("members")] public ICollection<string> Members;

        [JsonIgnore] public bool Valid =>
            Members != null &&
            Members.Count < 100 &&
            DateTimeFormats.TimestampExportFormat.Parse(Timestamp).Success;
    }

    public struct TupperboxConversionResult
    {
        public bool HadGroups;
        public bool HadIndividualTags;
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

            var members = Tuppers.Select(t => t.ToPluralKit(ref lastSetTag, ref output.HadIndividualTags,
                ref output.HadGroups)).ToList();
            
            // Nowadays we set each member's display name to their name + tag, so we don't set a global system tag
            output.System = new DataFileSystem
            {
                Members = members,
                Switches = new List<DataFileSwitch>()
            };
            return output;
        }
    }

    public struct TupperboxTupper
    {
        // TODO: support `message_count`, `nick` and `tag`
        [JsonProperty("name")] public string Name;
        [JsonProperty("avatar_url")] public string AvatarUrl;
        [JsonProperty("brackets")] public IList<string> Brackets;
        [JsonProperty("posts")] public int Posts; // Not supported by PK
        [JsonProperty("show_brackets")] public bool ShowBrackets;
        [JsonProperty("birthday")] public string Birthday;
        [JsonProperty("description")] public string Description;
        [JsonProperty("tag")] public string Tag;
        [JsonProperty("group_id")] public string GroupId; // Not supported by PK
        [JsonProperty("group_pos")] public int? GroupPos; // Not supported by PK

        [JsonIgnore] public bool Valid =>
            Name != null && Brackets != null && Brackets.Count % 2 == 0 &&
            (Birthday == null || DateTimeFormats.TimestampExportFormat.Parse(Birthday).Success);

        public PKMember ToPluralKit(ref string lastSetTag, ref bool multipleTags, ref bool hasGroup)
        {
            // If we've set a tag before and it's not the same as this one,
            // then we have multiple unique tags and we pass that flag back to the caller
            if (Tag != null && lastSetTag != null && lastSetTag != Tag) multipleTags = true;
            lastSetTag = Tag;

            // If this member is in a group, we have a (used) group and we flag that
            if (GroupId != null) hasGroup = true;

            // Brackets in Tupperbox format are arranged as a single array
            // [prefix1, suffix1, prefix2, suffix2, prefix3... etc]
            var tags = new List<ProxyTag>();
            for (var i = 0; i < Brackets.Count / 2; i++) 
                tags.Add(new ProxyTag(Brackets[i * 2], Brackets[i * 2 + 1]));

            // Convert birthday from ISO timestamp format to ISO date
            var convertedBirthdate = Birthday != null
                ? LocalDate.FromDateTime(DateTimeFormats.TimestampExportFormat.Parse(Birthday).Value.ToDateTimeUtc())
                : (LocalDate?) null;
            
            return new PKMember 
            {
                Hid = Guid.NewGuid().ToString(), // Note: this is only ever used for lookup purposes
                Name = Name,
                AvatarUrl = AvatarUrl,
                Birthday = convertedBirthdate,
                Description = Description,
                ProxyTags = tags,
                KeepProxy = ShowBrackets,
                DisplayName = Tag != null ? $"{Name} {Tag}" : null
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
