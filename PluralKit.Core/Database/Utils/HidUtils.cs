using System.Linq;
using System.Text.RegularExpressions;

namespace PluralKit.Core;

public static class HidUtils
{
    private static readonly Regex _hidRegex = new(@"^[a-zA-Z]{5,6}$");

    public static string? ParseHid(string input)
    {
        input = input.ToLower().Replace("-", null);
        if (!_hidRegex.IsMatch(input))
            return null;

        return input;
    }

    public static bool TryParseHid(this string input, out string hid)
    {
        hid = ParseHid(input);
        return hid != null;
    }

    public static string HidTransform(string input, bool split = false)
    {
        if (split && input.Length > 5)
        {
            var len = (int)Math.Floor(input.Length / 2.0);
            input = string.Concat(input.AsSpan(0, len), "-", input.AsSpan(len));
        }

        return input;
    }
}