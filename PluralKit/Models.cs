using System;
using Dapper.Contrib.Extensions;

namespace PluralKit
{
    [Table("systems")]
    public class PKSystem
    {
        [Key]
        public int Id { get; set; }
        public string Hid { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Tag { get; set; }
        public string AvatarUrl { get; set; }
        public string Token { get; set; }
        public DateTime Created { get; set; }
        public string UiTz { get; set; }

        public int MaxMemberNameLength => Tag != null ? 32 - Tag.Length - 1 : 32;
    }

    [Table("members")]
    public class PKMember
    {
        public int Id { get; set; }
        public string Hid { get; set; }
        public int System { get; set; }
        public string Color { get; set; }
        public string AvatarUrl { get; set; }
        public string Name { get; set; }
        public DateTime? Birthday { get; set; }
        public string Pronouns { get; set; }
        public string Description { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public DateTime Created { get; set; }

        /// Returns a formatted string representing the member's birthday, taking into account that a year of "0001" is hidden
        public string BirthdayString
        {
            get
            {
                if (Birthday == null) return null;
                if (Birthday?.Year == 1) return Birthday?.ToString("MMMM dd");
                return Birthday?.ToString("MMMM dd, yyyy");
            }
        }
    }
}