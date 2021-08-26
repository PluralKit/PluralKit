using System;
using System.Text.RegularExpressions;

namespace PluralKit.Core
{
    public abstract class PatchObject
    {
        public abstract UpdateQueryBuilder Apply(UpdateQueryBuilder b);

        public void AssertIsValid() {}

        protected bool AssertValid(string input, string name, int maxLength, Func<string, bool>? validate = null)
        {
            if (input.Length > maxLength)
                throw new FieldTooLongError(name, maxLength, input.Length);
            if (validate != null && !validate(input))
                throw new ValidationError(name);
            return true;
        }

        protected bool AssertValid(string input, string name, string pattern)
        {
            if (!Regex.IsMatch(input, pattern))
                throw new ValidationError(name);
            return true;
        }
    }

    public class ValidationError: Exception
    {
        public ValidationError(string message): base(message) { }
    }

    public class FieldTooLongError: ValidationError
    {
        public string Name;
        public int MaxLength;
        public int ActualLength;

        public FieldTooLongError(string name, int maxLength, int actualLength):
            base($"{name} too long ({actualLength} > {maxLength})")
        {
            Name = name;
            MaxLength = maxLength;
            ActualLength = actualLength;
        }
    }
}