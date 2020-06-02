using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Newtonsoft.Json;

using NodaTime;

using Serilog;

namespace PluralKit.Core
{
    public class DataFileService
    {
        private IDataStore _data;
        private IDatabase _db;
        private ILogger _logger;

        public DataFileService(ILogger logger, IDataStore data, IDatabase db)
        {
            _data = data;
            _db = db;
            _logger = logger.ForContext<DataFileService>();
        }

        public async Task<DataFileSystem> ExportSystem(PKSystem system)
        {
            // Export members
            var members = new List<DataFileMember>();
            var pkMembers = _data.GetSystemMembers(system); // Read all members in the system
            
            await foreach (var member in pkMembers.Select(m => new DataFileMember
            {
                Id = m.Hid,
                Name = m.Name,
                DisplayName = m.DisplayName,
                Description = m.Description,
                Birthday = m.Birthday?.FormatExport(),
                Pronouns = m.Pronouns,
                Color = m.Color,
                AvatarUrl = m.AvatarUrl,
                ProxyTags = m.ProxyTags,
                KeepProxy = m.KeepProxy,
                Created = m.Created.FormatExport(),
                MessageCount = m.MessageCount
            })) members.Add(member);

            // Export switches
            var switches = new List<DataFileSwitch>();
            var switchList = await _data.GetPeriodFronters(system, Instant.FromDateTimeUtc(DateTime.MinValue.ToUniversalTime()), SystemClock.Instance.GetCurrentInstant());
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
                Tag = system.TagSuffix,
                AvatarUrl = system.AvatarUrl,
                TimeZone = system.UiTz,
                Members = members,
                Switches = switches,
                Created = system.Created.FormatExport(),
                LinkedAccounts = (await _data.GetSystemAccounts(system)).ToList()
            };
        }

        private PKMember ConvertMember(PKSystem system, DataFileMember fileMember)
        {
            var newMember = new PKMember
            {
                Hid = fileMember.Id,
                System = system.Id,
                Name = fileMember.Name,
                DisplayName = fileMember.DisplayName,
                Description = fileMember.Description,
                Color = fileMember.Color,
                Pronouns = fileMember.Pronouns,
                AvatarUrl = fileMember.AvatarUrl,
                KeepProxy = fileMember.KeepProxy,
            };

            if (fileMember.Prefix != null || fileMember.Suffix != null)
                newMember.ProxyTags = new List<ProxyTag> {new ProxyTag(fileMember.Prefix, fileMember.Suffix)};
            else
                // Ignore proxy tags where both prefix and suffix are set to null (would be invalid anyway)
                newMember.ProxyTags = (fileMember.ProxyTags ?? new ProxyTag[] { }).Where(tag => !tag.IsEmpty).ToList();
                
            if (fileMember.Birthday != null)
            {
                var birthdayParse = DateTimeFormats.DateExportFormat.Parse(fileMember.Birthday);
                newMember.Birthday = birthdayParse.Success ? (LocalDate?)birthdayParse.Value : null;
            }

            return newMember;
        }
        
        public async Task<ImportResult> ImportSystem(DataFileSystem data, PKSystem system, ulong accountId)
        {
            var result = new ImportResult {
                AddedNames = new List<string>(),
                ModifiedNames = new List<string>(),
                System = system,
                Success = true // Assume success unless indicated otherwise
            };
            
            // If we don't already have a system to save to, create one
            if (system == null)
            {
                system = result.System = await _data.CreateSystem(data.Name);
                await _data.AddAccount(system, accountId);
            }
            
            // Apply system info
            system.Name = data.Name;
            if (data.Description != null) system.Description = data.Description;
            if (data.Tag != null) system.TagSuffix = data.Tag;
            if (data.AvatarUrl != null) system.AvatarUrl = data.AvatarUrl;
            if (data.TimeZone != null) system.UiTz = data.TimeZone ?? "UTC";
            await _data.SaveSystem(system);
            
            // -- Member/switch import --
            await using var conn = await _db.Obtain();
            await using (var imp = await BulkImporter.Begin(system, conn))
            {
                // Tally up the members that didn't exist before, and check member count on import
                // If creating the unmatched members would put us over the member limit, abort before creating any members
                var memberCountBefore = await _data.GetSystemMemberCount(system.Id, true);
                var membersToAdd = data.Members.Count(m => imp.IsNewMember(m.Id, m.Name));
                if (memberCountBefore + membersToAdd > Limits.MaxMemberCount)
                {
                    result.Success = false;
                    result.Message = $"Import would exceed the maximum number of members ({Limits.MaxMemberCount}).";
                    return result;
                }
                
                async Task DoImportMember(BulkImporter imp, DataFileMember fileMember)
                {
                    var isCreatingNewMember = imp.IsNewMember(fileMember.Id, fileMember.Name);

                    // Use the file member's id as the "unique identifier" for the importing (actual value is irrelevant but needs to be consistent)
                    _logger.Debug(
                        "Importing member with identifier {FileId} to system {System} (is creating new member? {IsCreatingNewMember})",
                        fileMember.Id, system.Id, isCreatingNewMember);
                    var newMember = await imp.AddMember(fileMember.Id, ConvertMember(system, fileMember));

                    if (isCreatingNewMember)
                        result.AddedNames.Add(newMember.Name);
                    else
                        result.ModifiedNames.Add(newMember.Name);
                }
                
                // Can't parallelize this because we can't reuse the same connection/tx inside the importer
                foreach (var m in data.Members) 
                    await DoImportMember(imp, m);
                
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
        public ICollection<string> AddedNames;
        public ICollection<string> ModifiedNames;
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
        [JsonProperty("members")] public ICollection<DataFileMember> Members;
        [JsonProperty("switches")] public ICollection<DataFileSwitch> Switches;
        [JsonProperty("accounts")] public ICollection<ulong> LinkedAccounts;
        [JsonProperty("created")] public string Created;

        private bool TimeZoneValid => TimeZone == null || DateTimeZoneProviders.Tzdb.GetZoneOrNull(TimeZone) != null;

        [JsonIgnore] public bool Valid =>
            TimeZoneValid &&
            Members != null &&
            Members.Count <= Limits.MaxMemberCount &&
            Members.All(m => m.Valid) &&
            Switches != null &&
            Switches.Count < 10000 &&
            Switches.All(s => s.Valid) &&
            !Name.IsLongerThan(Limits.MaxSystemNameLength) &&
            !Description.IsLongerThan(Limits.MaxDescriptionLength) &&
            !Tag.IsLongerThan(Limits.MaxSystemTagLength) &&
            !AvatarUrl.IsLongerThan(1000);
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
        
        // For legacy single-tag imports
        [JsonProperty("prefix")] [JsonIgnore] public string Prefix;
        [JsonProperty("suffix")] [JsonIgnore] public string Suffix;
        
        // ^ is superseded by v
        [JsonProperty("proxy_tags")] public ICollection<ProxyTag> ProxyTags;

        [JsonProperty("keep_proxy")] public bool KeepProxy;
        [JsonProperty("message_count")] public int MessageCount;
        [JsonProperty("created")] public string Created;

        [JsonIgnore] public bool Valid =>
            Name != null &&
            !Name.IsLongerThan(Limits.MaxMemberNameLength) &&
            !DisplayName.IsLongerThan(Limits.MaxMemberNameLength) &&
            !Description.IsLongerThan(Limits.MaxDescriptionLength) &&
            !Pronouns.IsLongerThan(Limits.MaxPronounsLength) &&
            (Color == null || Regex.IsMatch(Color, "[0-9a-fA-F]{6}")) &&
            (Birthday == null || DateTimeFormats.DateExportFormat.Parse(Birthday).Success) &&

            // Sanity checks
            !AvatarUrl.IsLongerThan(1000) &&

            // Older versions have Prefix and Suffix as fields, meaning ProxyTags is null
            (ProxyTags == null || ProxyTags.Count < 100 &&
                ProxyTags.All(t => !t.ProxyString.IsLongerThan(100))) &&
            !Prefix.IsLongerThan(100) && !Suffix.IsLongerThan(100);
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

        public DataFileMember ToPluralKit(ref string lastSetTag, ref bool multipleTags, ref bool hasGroup)
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
            
            return new DataFileMember
            {
                Id = Guid.NewGuid().ToString(), // Note: this is only ever used for lookup purposes
                Name = Name,
                AvatarUrl = AvatarUrl,
                Birthday = convertedBirthdate?.FormatExport(),
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