using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;

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
            Birthdate = member.Birthday?.ToString(Formats.DateExportFormat, null),
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
            var result = new ImportResult { AddedNames = new List<string>(), ModifiedNames = new List<string>() };
            
            // If we don't already have a system to save to, create one
            if (system == null) system = await _systems.Create(data.Name);
            
            // Apply system info
            system.Name = data.Name;
            system.Description = data.Description;
            system.Tag = data.Tag;
            system.AvatarUrl = data.AvatarUrl;
            system.UiTz = data.TimeZone ?? "UTC";
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
                member.Description = dataMember.Description;
                member.Color = dataMember.Color;
                member.AvatarUrl = dataMember.AvatarUrl;
                member.Prefix = dataMember.Prefix;
                member.Suffix = dataMember.Suffix;

                var birthdayParse = LocalDatePattern.CreateWithInvariantCulture(Formats.DateExportFormat).Parse(dataMember.Birthdate);
                member.Birthday = birthdayParse.Success ? (LocalDate?) birthdayParse.Value : null;
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
        [JsonProperty("id")]
        public string Id;
        
        [JsonProperty("name")]
        public string Name;
        
        [JsonProperty("description")]
        public string Description;
        
        [JsonProperty("tag")]
        public string Tag;
        
        [JsonProperty("avatar_url")]
        public string AvatarUrl;
        
        [JsonProperty("timezone")]
        public string TimeZone;

        [JsonProperty("members")]
        public ICollection<DataFileMember> Members;
        
        [JsonProperty("switches")]
        public ICollection<DataFileSwitch> Switches;
        
        [JsonProperty("accounts")]
        public ICollection<ulong> LinkedAccounts;

        [JsonProperty("created")]
        public string Created;
        
        private bool TimeZoneValid => TimeZone == null || DateTimeZoneProviders.Tzdb.GetZoneOrNull(TimeZone) != null;
        
        [JsonIgnore]
        public bool Valid => TimeZoneValid && Members.All(m => m.Valid);
    }

    public struct DataFileMember
    {
        [JsonProperty("id")]
        public string Id;
        
        [JsonProperty("name")]
        public string Name;
        
        [JsonProperty("description")]
        public string Description;
        
        [JsonProperty("birthday")]
        public string Birthdate;
        
        [JsonProperty("pronouns")]
        public string Pronouns;
        
        [JsonProperty("color")]
        public string Color;
        
        [JsonProperty("avatar_url")]
        public string AvatarUrl;
        
        [JsonProperty("prefix")]
        public string Prefix;
        
        [JsonProperty("suffix")]
        public string Suffix;
        
        [JsonProperty("message_count")]
        public int MessageCount;

        [JsonProperty("created")]
        public string Created;

        [JsonIgnore]
        public bool Valid => Name != null;
    }

    public struct DataFileSwitch
    {
        [JsonProperty("timestamp")]
        public string Timestamp;
        
        [JsonProperty("members")]
        public ICollection<string> Members;
    }
}