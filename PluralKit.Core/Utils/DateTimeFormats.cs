using NodaTime;
using NodaTime.Text;

namespace PluralKit.Core {
    public static class DateTimeFormats
    {
        public static IPattern<Instant> TimestampExportFormat = InstantPattern.ExtendedIso;
        public static IPattern<LocalDate> DateExportFormat = LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd");
        
        // We create a composite pattern that only shows the two most significant things
        // eg. if we have something with nonzero day component, we show <x>d <x>h, but if it's
        // a smaller duration we may only bother with showing <x>h <x>m or <x>m <x>s
        public static IPattern<Duration> DurationFormat = new CompositePatternBuilder<Duration>
        {
            {DurationPattern.CreateWithInvariantCulture("s's'"), d => true},
            {DurationPattern.CreateWithInvariantCulture("m'm' s's'"), d => d.Minutes > 0},
            {DurationPattern.CreateWithInvariantCulture("H'h' m'm'"), d => d.Hours > 0},
            {DurationPattern.CreateWithInvariantCulture("D'd' h'h'"), d => d.Days > 0}
        }.Build();
        
        public static IPattern<LocalDateTime> LocalDateTimeFormat = LocalDateTimePattern.CreateWithInvariantCulture("yyyy-MM-dd HH:mm:ss");
        public static IPattern<ZonedDateTime> ZonedDateTimeFormat = ZonedDateTimePattern.CreateWithInvariantCulture("yyyy-MM-dd HH:mm:ss x", DateTimeZoneProviders.Tzdb);

        public static string FormatExport(this Instant instant) => TimestampExportFormat.Format(instant);
        public static string FormatExport(this LocalDate date) => DateExportFormat.Format(date);
        public static string FormatZoned(this ZonedDateTime zdt) => ZonedDateTimeFormat.Format(zdt);
        public static string FormatZoned(this Instant i, DateTimeZone zone) => i.InZone(zone).FormatZoned();
        public static string FormatZoned(this Instant i, PKSystem sys) => i.FormatZoned(sys.Zone);
        public static string FormatDuration(this Duration d) => DurationFormat.Format(d);
    }
}