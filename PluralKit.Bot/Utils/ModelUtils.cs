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

    public static string Reference(this PKMember member, Context ctx) => EntityReference(member.Hid, member.NameFor(ctx));
    public static string Reference(this PKGroup group, Context ctx) => EntityReference(group.Hid, group.NameFor(ctx));

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