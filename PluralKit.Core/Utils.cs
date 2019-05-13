using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NodaTime;
using NodaTime.Text;


namespace PluralKit
{
    public static class Utils
    {
        public static string GenerateHid()
        {
            var rnd = new Random();
            var charset = "abcdefghijklmnopqrstuvwxyz";
            string hid = "";
            for (int i = 0; i < 5; i++)
            {
                hid += charset[rnd.Next(charset.Length)];
            }
            return hid;
        }

        public static string Truncate(this string str, int maxLength, string ellipsis = "...") {
            if (str.Length < maxLength) return str;
            return str.Substring(0, maxLength - ellipsis.Length) + ellipsis;
        }

        public static bool IsLongerThan(this string str, int length)
        {
            if (str != null) return str.Length > length;
            return false;
        }

        public static Duration? ParsePeriod(string str)
        {
            
            Duration d = Duration.Zero;
            
            foreach (Match match in Regex.Matches(str, "(\\d{1,3})(\\w)"))
            {
                var amount = int.Parse(match.Groups[1].Value);
                var type = match.Groups[2].Value;

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
                "yyyy/MM/dd"   // 2019/01/01
            }.ToList();

            if (allowNullYear) patterns.AddRange(new[]
            {
                "MMM d",        // Jan 1
                "MMMM d",       // January 1
                "MM-dd",        // 01-01
                "MM dd",        // 01 01
                "MM/dd"         // 01-01
            });

            // Giving a template value so year will be parsed as 0001 if not present
            // This means we can later disambiguate whether a null year was given
            // TODO: should we be using invariant culture here?
            foreach (var pattern in patterns.Select(p => LocalDatePattern.CreateWithInvariantCulture(p).WithTemplateValue(new LocalDate(0001, 1, 1))))
            {
                var result = pattern.Parse(str);
                if (result.Success) return result.Value;
            }

            return null;
        }
    }

    public static class Emojis {
        public static readonly string Warn = "\u26A0";
        public static readonly string Success = "\u2705";
        public static readonly string Error = "\u274C";
        public static readonly string Note = "\u2757";
        public static readonly string ThumbsUp = "\U0001f44d";
    }
}