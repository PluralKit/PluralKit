using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core;

public abstract class PatchObject
{
    public List<ValidationError> Errors = new();
    public abstract Query Apply(Query q);

    public void AssertIsValid() { }

    protected void AssertValid(string input, string name, int maxLength, Func<string, bool>? validate = null)
    {
        if (input.Length > maxLength)
            Errors.Add(new FieldTooLongError(name, maxLength, input.Length));
        if (validate != null && !validate(input))
            Errors.Add(new ValidationError(name));
    }

    protected void AssertValid(string input, string name, string pattern)
    {
        if (!Regex.IsMatch(input, pattern))
            Errors.Add(new ValidationError(name));
    }

    public PrivacyLevel ParsePrivacy(JObject o, string propertyName)
    {
        var input = o.Value<string>(propertyName);

        if (input == null) return PrivacyLevel.Public;
        if (input == "") return PrivacyLevel.Private;
        if (input == "private") return PrivacyLevel.Private;
        if (input == "public") return PrivacyLevel.Public;

        Errors.Add(new ValidationError(propertyName));
        // unused, but the compiler will complain if this isn't here
        return PrivacyLevel.Private;
    }
}