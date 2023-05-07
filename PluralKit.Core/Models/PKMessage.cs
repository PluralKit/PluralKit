using Newtonsoft.Json.Linq;

using NodaTime;

namespace PluralKit.Core;

public class PKMessage
{
    public ulong Mid { get; set; }
    public ulong? Guild { get; set; } // null value means "no data" (ie. from before this field being added)
    public ulong Channel { get; set; }
    public MemberId? Member { get; set; }
    public ulong Sender { get; set; }
    public ulong? OriginalMid { get; set; }
}

public static class PKMessageExt
{
    public static string JumpLink(this PKMessage msg) =>
        $"https://discord.com/channels/{msg.Guild!.Value}/{msg.Channel}/{msg.Mid}";
}

public class FullMessage
{
    public PKMessage Message;
    public PKMember? Member;
    public PKSystem? System;

    public JObject ToJson(LookupContext ctx)
    {
        var o = new JObject();

        o.Add("timestamp", Instant.FromUnixTimeMilliseconds((long)(Message.Mid >> 22) + 1420070400000).ToString());
        o.Add("id", Message.Mid.ToString());
        o.Add("original", Message.OriginalMid.ToString());
        o.Add("sender", Message.Sender.ToString());
        o.Add("channel", Message.Channel.ToString());
        o.Add("guild", Message.Guild?.ToString());
        o.Add("system", System?.ToJson(ctx));
        o.Add("member", Member?.ToJson(ctx));

        return o;
    }
}