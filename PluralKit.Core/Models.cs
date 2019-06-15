using Dapper.Contrib.Extensions;
using NodaTime;
using NodaTime.Text;

namespace PluralKit
{
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
        public Instant Created { get; set; }
        public string UiTz { get; set; }

        public int MaxMemberNameLength => Tag != null ? 32 - Tag.Length - 1 : 32;

        public DateTimeZone Zone => DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz);
    }

    public class PKMember
    {
        public int Id { get; set; }
        public string Hid { get; set; }
        public int System { get; set; }
        public string Color { get; set; }
        public string AvatarUrl { get; set; }
        public string Name { get; set; }
        public LocalDate? Birthday { get; set; }
        public string Pronouns { get; set; }
        public string Description { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public Instant Created { get; set; }

        /// Returns a formatted string representing the member's birthday, taking into account that a year of "0001" is hidden
        public string BirthdayString
        {
            get
            {
                if (Birthday == null) return null;

                var format = LocalDatePattern.CreateWithInvariantCulture("MMM dd, yyyy");
                if (Birthday?.Year == 1) format = LocalDatePattern.CreateWithInvariantCulture("MMM dd");
                return format.Format(Birthday.Value);
            }
        }

        public bool HasProxyTags => Prefix != null || Suffix != null;
        public string ProxyString => $"{Prefix ?? ""}text{Suffix ?? ""}";
    }

    public class PKSwitch
    {
        public int Id { get; set; }
        public int System { get; set; }
        public Instant Timestamp { get; set; }
    }

    public class PKSwitchMember
    {
        public int Id { get; set; }
        public int Switch { get; set; }
        public int Member { get; set; }
    }
}