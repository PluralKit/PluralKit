﻿using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PluralKit.Core
{
    public static class StringUtils
    {
        public static string GenerateToken()
        {
            var buf = new byte[48]; // Results in a 64-byte Base64 string (no padding)
            new RNGCryptoServiceProvider().GetBytes(buf);
            return Convert.ToBase64String(buf);
        }

        public static bool IsLongerThan(this string str, int length)
        {
            if (str != null) return str.Length > length;
            return false;
        }
        
        public static string ExtractCountryFlag(string flag)
        {
            if (flag.Length != 4) return null;
            try
            {
                var cp1 = char.ConvertToUtf32(flag, 0);
                var cp2 = char.ConvertToUtf32(flag, 2);
                if (cp1 < 0x1F1E6 || cp1 > 0x1F1FF) return null;
                if (cp2 < 0x1F1E6 || cp2 > 0x1F1FF) return null;
                return $"{(char) (cp1 - 0x1F1E6 + 'A')}{(char) (cp2 - 0x1F1E6 + 'A')}";
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
        
        public static string NullIfEmpty(this string input)
        {
            if (input == null) return null;
            if (input.Trim().Length == 0) return null;
            return input;
        }

        public static bool EmptyOrNull(this string input)
        {
            if (input == null) return true;
            if (input.Trim().Length == 0) return true;
            return false;
        }

        public static string NormalizeLineEndSpacing(this string input)
        {
            // iOS has a weird issue on embeds rendering newlines when there are spaces *just before* it
            // so we remove 'em all :)
            return Regex.Replace(input, " *\n", "\n");
        }
    }
}