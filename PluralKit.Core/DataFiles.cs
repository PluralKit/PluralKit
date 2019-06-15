using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Text;

namespace PluralKit.Bot
{
    public class DataFileService
    {
        private SystemStore _systems;
        private MemberStore _members;
        private SwitchStore _switches;

        public DataFileService(SystemStore systems, MemberStore members, SwitchStore switches)
        {
            _systems = systems;
            _members = members;
            _switches = switches;
        }

        public async Task<DataFileSystem> ExportSystem(PKSystem system)
        {
            var members = new List<DataFileMember>();
            foreach (var member in await _members.GetBySystem(system)) members.Add(await ExportMember(member));

            var switches = new List<DataFileSwitch>();
            foreach (var sw in await _switches.GetSwitches(system, 999999)) switches.Add(await ExportSwitch(sw));

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
                Created = system.Created.ToString(Formats.TimestampExportFormat, null),
                LinkedAccounts = (await _systems.GetLinkedAccountIds(system)).ToList()
            };
        }

        private async Task<DataFileMember> ExportMember(PKMember member) => new DataFileMember
        {
            Id = member.Hid,
            Name = member.Name,
            Description = member.Description,
            Birthday = member.Birthday?.ToString(Formats.DateExportFormat, null),
            Pronouns = member.Pronouns,
            Color = member.Color,
            AvatarUrl = member.AvatarUrl,
            Prefix = member.Prefix,
            Suffix = member.Suffix,
            Created = member.Created.ToString(Formats.TimestampExportFormat, null),
            MessageCount = await _members.MessageCount(member)
        };

        private async Task<DataFileSwitch> ExportSwitch(PKSwitch sw) => new DataFileSwitch
        {
            Members = (await _switches.GetSwitchMembers(sw)).Select(m => m.Hid).ToList(),
            Timestamp = sw.Timestamp.ToString(Formats.TimestampExportFormat, null)
        };

        public async Task<ImportResult> ImportSystem(DataFileSystem data, PKSystem system)
        {
            var result = new ImportResult {AddedNames = new List<string>(), ModifiedNames = new List<string>()};

            // If we don't already have a system to save to, create one
            if (system == null) system = await _systems.Create(data.Name);

            // Apply system info
            system.Name = data.Name;
            if (data.Description != null) system.Description = data.Description;
            if (data.Tag != null) system.Tag = data.Tag;
            if (data.AvatarUrl != null) system.AvatarUrl = data.AvatarUrl;
            if (data.TimeZone != null) system.UiTz = data.TimeZone ?? "UTC";
            await _systems.Save(system);

            // Apply members
            // TODO: parallelize?
            foreach (var dataMember in data.Members)
            {
                // If member's given an ID, we try to look up the member with the given ID
                PKMember member = null;
                if (dataMember.Id != null)
                {
                    member = await _members.GetByHid(dataMember.Id);

                    // ...but if it's a different system's member, we just make a new one anyway
                    if (member != null && member.System != system.Id) member = null;
                }

                // Try to look up by name, too
                if (member == null) member = await _members.GetByName(system, dataMember.Name);

                // And if all else fails (eg. fresh import from Tupperbox, etc) we just make a member lol
                if (member == null)
                {
                    member = await _members.Create(system, dataMember.Name);
                    result.AddedNames.Add(dataMember.Name);
                }
                else
                {
                    result.ModifiedNames.Add(dataMember.Name);
                }

                // Apply member info
                member.Name = dataMember.Name;
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
                    var birthdayParse = LocalDatePattern.CreateWithInvariantCulture(Formats.DateExportFormat)
                        .Parse(dataMember.Birthday);
                    member.Birthday = birthdayParse.Success ? (LocalDate?) birthdayParse.Value : null;
                }

                await _members.Save(member);
            }

            // TODO: import switches, too?

            result.System = system;
            return result;
        }
    }

    public struct ImportResult
    {
        public ICollection<string> AddedNames;
        public ICollection<string> ModifiedNames;
        public PKSystem System;
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
                Name = Name,
                AvatarUrl = AvatarUrl,
                Birthday = Birthday,
                Description = Description,
                Prefix = Brackets.FirstOrDefault(),
                Suffix = Brackets.Skip(1).FirstOrDefault() // TODO: can Tupperbox members have no proxies at all?
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