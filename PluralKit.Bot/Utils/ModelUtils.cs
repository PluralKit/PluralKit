using System.Text.RegularExpressions;

using PluralKit.Core;

namespace PluralKit.Bot;

public static class ModelUtils
{
    public static string NameFor(this PKMember member, Context ctx) =>
        member.NameFor(ctx.LookupContextFor(member.System));

    public static string NameFor(this PKGroup group, Context ctx) =>
        group.NameFor(ctx.LookupContextFor(group.System));

    public static string NameFor(this PKSystem system, Context ctx) =>
        system.NameFor(ctx.LookupContextFor(system.Id));

    public static string AvatarFor(this PKMember member, Context ctx) =>
        member.AvatarFor(ctx.LookupContextFor(member.System)).TryGetCleanCdnUrl();

    public static string IconFor(this PKGroup group, Context ctx) =>
        group.IconFor(ctx.LookupContextFor(group.System)).TryGetCleanCdnUrl();

    public static string DisplayName(this PKMember member) =>
        member.DisplayName ?? member.Name;

    public static string Reference(this PKMember member, Context ctx) => EntityReference(member.DisplayHid(ctx.Config), member.NameFor(ctx));
    public static string Reference(this PKGroup group, Context ctx) => EntityReference(group.DisplayHid(ctx.Config), group.NameFor(ctx));


    public static string DisplayHid(this PKSystem system, SystemConfig? cfg = null, bool isList = false) => HidTransform(system.Hid, cfg, isList);
    public static string DisplayHid(this PKGroup group, SystemConfig? cfg = null, bool isList = false, bool shouldPad = true) => HidTransform(group.Hid, cfg, isList, shouldPad);
    public static string DisplayHid(this PKMember member, SystemConfig? cfg = null, bool isList = false, bool shouldPad = true) => HidTransform(member.Hid, cfg, isList, shouldPad);
    private static string HidTransform(string hid, SystemConfig? cfg = null, bool isList = false, bool shouldPad = true) =>
        HidUtils.HidTransform(
            hid,
            cfg != null && cfg.HidDisplaySplit,
            cfg != null && cfg.HidDisplayCaps,
            isList && shouldPad ? (cfg?.HidListPadding ?? SystemConfig.HidPadFormat.None) : SystemConfig.HidPadFormat.None // padding only on lists
        );

    private static string EntityReference(string hid, string name)
    {
        bool IsSimple(string s) =>
            // No spaces, no symbols, allow single quote but not at the start
            Regex.IsMatch(s, "^[\\w\\d\\-_'?]+$") && !s.StartsWith("'");

        // If it's very long (>25 chars), always use hid
        if (name.Length >= 25)
            return hid;

        // If name is "simple" just use that
        if (IsSimple(name))
            return name;

        // If three or fewer "words" and they're all simple individually, quote them
        var words = name.Split(' ');
        if (words.Length <= 3 && words.All(w => w.Length > 0 && IsSimple(w)))
            // Words with double quotes are never "simple" so we're safe to naive-quote here
            return $"\"{name}\"";

        // Otherwise, just use hid
        return hid;
    }
}