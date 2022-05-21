using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core;

public class AccountPatch: PatchObject
{
    public Partial<ulong> DmChannel { get; set; }
    public Partial<bool> AllowAutoproxy { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("dm_channel", DmChannel)
        .With("allow_autoproxy", AllowAutoproxy)
    );

    public JObject ToJson()
    {
        var o = new JObject();

        if (AllowAutoproxy.IsPresent)
            o.Add("allow_autoproxy", AllowAutoproxy.Value);

        return o;
    }
}