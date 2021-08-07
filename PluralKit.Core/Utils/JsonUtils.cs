using System;

namespace PluralKit.Core
{
    internal static class JsonUtils
    {
        public static string BoundsCheckField(this string input, int maxLength, string nameInError)
        {
            if (input != null && input.Length > maxLength)
                throw new JsonModelParseError($"{nameInError} too long ({input.Length} > {maxLength}).");
            return input;
        }
    }
    
    public class JsonModelParseError: Exception
    {
        public JsonModelParseError(string message): base(message) { }
    }
}