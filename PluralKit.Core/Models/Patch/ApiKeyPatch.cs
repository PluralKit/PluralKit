using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core;

public class ApiKeyPatch: PatchObject
{
    public Partial<string> Name { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("name", Name)
    );

    public JObject ToJson()
    {
        var o = new JObject();

        if (Name.IsPresent)
            o.Add("name", Name.Value);

        return o;
    }
}