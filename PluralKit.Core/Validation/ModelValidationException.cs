#nullable enable
using System;

namespace PluralKit.Core.Validation
{
    public class ModelValidationException: Exception
    {
        public string Property { get; set; }

        public ModelValidationException(string property, string? message = null): base(message)
        {
            Property = property;
        }
    }
}