using Newtonsoft.Json.Linq;

using NodaTime;

using SqlKata;

namespace PluralKit.Core;

public class AutoproxyPatch: PatchObject
{
    public Partial<AutoproxyMode> AutoproxyMode { get; set; }
    public Partial<MemberId?> AutoproxyMember { get; set; }

    public Partial<Instant> LastLatchTimestamp { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("autoproxy_mode", AutoproxyMode)
        .With("autoproxy_member", AutoproxyMember)
        .With("last_latch_timestamp", LastLatchTimestamp)
    );

    public new void AssertIsValid()
    {
        // this is checked in FromJson
        // not really the best way to do this, maybe fix at some point?
        if ((int?)AutoproxyMode.Value == -1)
            Errors.Add(new("autoproxy_mode"));
    }

    public static AutoproxyPatch FromJson(JObject o, MemberId? autoproxyMember = null)
    {
        var p = new AutoproxyPatch();

        if (o.ContainsKey("autoproxy_mode"))
        {
            var (autoproxyMode, error) = o.Value<JToken>("autoproxy_mode").ParseAutoproxyMode();
            if (error != null)
                p.AutoproxyMode = Partial<AutoproxyMode>.Present((AutoproxyMode)(-1));
            else
                p.AutoproxyMode = autoproxyMode.Value;
        }

        p.AutoproxyMember = autoproxyMember ?? Partial<MemberId?>.Absent;

        return p;
    }

    public JObject ToJson(ulong? guild_id, ulong? channel_id, string? memberId = null)
    {
        var o = new JObject();

        o.Add("guild_id", guild_id?.ToString());
        o.Add("channel_id", channel_id?.ToString());

        if (AutoproxyMode.IsPresent)
            o.Add("autoproxy_mode", AutoproxyMode.Value.ToString().ToLower());

        if (AutoproxyMember.IsPresent)
            o.Add("autoproxy_member", memberId);

        if (LastLatchTimestamp.IsPresent)
            o.Add("last_latch_timestamp", LastLatchTimestamp.Value.FormatExport());

        return o;
    }
}