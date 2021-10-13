using System;

using Newtonsoft.Json;

namespace PluralKit.Core
{
    public class ValidationError
    {
        public string Key;
        public string? Text;
        public ValidationError(string key, string? text = null)
        {
            Key = key;
            Text = text;
        }
    }

    public class FieldTooLongError: ValidationError
    {
        public int MaxLength;
        public int ActualLength;

        public FieldTooLongError(string key, int maxLength, int actualLength) : base(key)
        {
            MaxLength = maxLength;
            ActualLength = actualLength;
        }
    }
}