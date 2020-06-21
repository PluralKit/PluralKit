using System.Linq;
using System.Text.RegularExpressions;

using NodaTime;
using NodaTime.Text;

namespace PluralKit.Core
{
    public class DateUtils
    {
        public static Duration? ParsePeriod(string str)
        {
            Duration d = Duration.Zero;
            
            foreach (Match match in Regex.Matches(str, "(\\d{1,6})(\\w)"))
            {
                var amount = int.Parse(match.Groups[1].Value);
                var type = match.Groups[2].Value.ToLowerInvariant();

                if (type == "w") d += Duration.FromDays(7) * amount;
                else if (type == "d") d += Duration.FromDays(1) * amount;
                else if (type == "h") d += Duration.FromHours(1) * amount;
                else if (type == "m") d += Duration.FromMinutes(1) * amount;
                else if (type == "s") d += Duration.FromSeconds(1) * amount;
                else return null;
            }

            if (d == Duration.Zero) return null;
            return d;
        }

        public static LocalDate? ParseDate(string str, bool allowNullYear = false)
        {
            // NodaTime can't parse constructs like "1st" and "2nd" so we quietly replace those away
            // Gotta make sure to do the regex otherwise we'll catch things like the "st" in "August" too
            str = Regex.Replace(str, "(\\d+)(st|nd|rd|th)", "$1");
            
            var patterns = new[]
            {
                "MMM d yyyy",   // Jan 1 2019
                "MMM d, yyyy",  // Jan 1, 2019
                "MMMM d yyyy",  // January 1 2019
                "MMMM d, yyyy", // January 1, 2019
                "yyyy-MM-dd",   // 2019-01-01
                "yyyy MM dd",   // 2019 01 01
                "yyyy/MM/dd"    // 2019/01/01
            }.ToList();

            if (allowNullYear) patterns.AddRange(new[]
            {
                "MMM d",        // Jan 1
                "MMMM d",       // January 1
                "MM-dd",        // 01-01
                "MM dd",        // 01 01
                "MM/dd"         // 01/01
            });

            // Giving a template value so year will be parsed as 0004 if not present
            // This means we can later disambiguate whether a null year was given
            // We use the basis year 0004 (rather than, say, 0001) because 0004 is a leap year in the Gregorian calendar
            // which means the date "Feb 29, 0004" is a valid date. 0001 is still accepted as a null year for legacy reasons.
            // TODO: should we be using invariant culture here?
            foreach (var pattern in patterns.Select(p => LocalDatePattern.CreateWithInvariantCulture(p).WithTemplateValue(new LocalDate(0004, 1, 1))))
            {
                var result = pattern.Parse(str);
                if (result.Success) return result.Value;
            }

            return null;
        }

        public static ZonedDateTime? ParseDateTime(string str, bool nudgeToPast = false, DateTimeZone zone = null)
        {
            if (zone == null) zone = DateTimeZone.Utc;
            
            // Find the current timestamp in the given zone, find the (naive) midnight timestamp, then put that into the same zone (and make it naive again)
            // Should yield a <current *local @ zone* date> 12:00:00 AM.
            var now = SystemClock.Instance.GetCurrentInstant().InZone(zone).LocalDateTime;
            var midnight = now.Date.AtMidnight();
            
            // First we try to parse the string as a relative time using the period parser
            var relResult = ParsePeriod(str);
            if (relResult != null)
            {
                // if we can, we just subtract that amount from the 
                return now.InZoneLeniently(zone).Minus(relResult.Value);
            }

            var timePatterns = new[]
            {
                "H:mm",         // 4:30
                "HH:mm",        // 23:30
                "H:mm:ss",      // 4:30:29
                "HH:mm:ss",     // 23:30:29
                "h tt",         // 2 PM
                "htt",          // 2PM
                "h:mm tt",      // 4:30 PM
                "h:mmtt",       // 4:30PM
                "h:mm:ss tt",   // 4:30:29 PM
                "h:mm:sstt",    // 4:30:29PM
                "hh:mm tt",     // 11:30 PM
                "hh:mmtt",      // 11:30PM
                "hh:mm:ss tt",   // 11:30:29 PM
                "hh:mm:sstt"   // 11:30:29PM
            };

            var datePatterns = new[]
            {
                "MMM d yyyy",   // Jan 1 2019
                "MMM d, yyyy",  // Jan 1, 2019
                "MMMM d yyyy",  // January 1 2019
                "MMMM d, yyyy", // January 1, 2019
                "yyyy-MM-dd",   // 2019-01-01
                "yyyy MM dd",   // 2019 01 01
                "yyyy/MM/dd",   // 2019/01/01
                "MMM d",        // Jan 1
                "MMMM d",       // January 1
                "MM-dd",        // 01-01
                "MM dd",        // 01 01
                "MM/dd"         // 01-01
            };
            
            // First, we try all the timestamps that only have a time
            foreach (var timePattern in timePatterns)
            {
                var pat = LocalDateTimePattern.CreateWithInvariantCulture(timePattern).WithTemplateValue(midnight);
                var result = pat.Parse(str);
                if (result.Success)
                {
                    // If we have a successful match and we need a time in the past, we try to shove a future-time a date before
                    // Example: "4:30 pm" at 3:30 pm likely refers to 4:30 pm the previous day
                    var val = result.Value;
                    
                    // If we need to nudge, we just subtract a day. This only occurs when we're parsing specifically *just time*, so
                    // we know we won't nudge it by more than a day since we use today's midnight timestamp as a date template.
                    
                    // Since this is a naive datetime, this ensures we're actually moving by one calendar day even if 
                    // DST changes occur, since they'll be resolved later wrt. the right side of the boundary
                    if (val > now && nudgeToPast) val = val.PlusDays(-1);
                    return val.InZoneLeniently(zone);
                }
            }
            
            // Then we try specific date+time combinations, both date first and time first, with and without commas
            foreach (var timePattern in timePatterns)
            {
                foreach (var datePattern in datePatterns)
                {
                    foreach (var patternStr in new[]
                    {
                        $"{timePattern}, {datePattern}", $"{datePattern}, {timePattern}",
                        $"{timePattern} {datePattern}", $"{datePattern} {timePattern}"
                    })
                    {
                        var pattern = LocalDateTimePattern.CreateWithInvariantCulture(patternStr).WithTemplateValue(midnight);
                        var res = pattern.Parse(str);
                        if (res.Success) return res.Value.InZoneLeniently(zone);
                    }
                }
            }
            
            // Finally, just date patterns, still using midnight as the template
            foreach (var datePattern in datePatterns)
            {
                var pat = LocalDateTimePattern.CreateWithInvariantCulture(datePattern).WithTemplateValue(midnight);
                var res = pat.Parse(str);
                if (res.Success) return res.Value.InZoneLeniently(zone);
            }

            // Still haven't parsed something, we just give up lmao
            return null;
        }
    }
}