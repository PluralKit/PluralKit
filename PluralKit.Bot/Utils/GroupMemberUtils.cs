using PluralKit.Core;

namespace PluralKit.Bot;

public static class GroupMemberUtils
{
    public static string GenerateResponse(Groups.AddRemoveOperation action, int memberCount, int groupCount,
                                          int actionedOn, int notActionedOn)
    {
        var op = action;

        var actionStr = action == Groups.AddRemoveOperation.Add ? "added to" : "removed from";
        var containStr = action == Groups.AddRemoveOperation.Add ? "in" : "not in";
        var emojiStr = actionedOn > 0 ? Emojis.Success : Emojis.Error;

        var memberPlural = memberCount > 1;
        var groupPlural = groupCount > 1;

        // sanity checking: we can't add multiple groups to multiple members (at least for now)
        if (memberPlural && groupPlural)
            throw new ArgumentOutOfRangeException();

        // sanity checking: we can't act/not act on a different number of entities than we have
        if (memberPlural && actionedOn + notActionedOn != memberCount)
            throw new ArgumentOutOfRangeException();
        if (groupPlural && actionedOn + notActionedOn != groupCount)
            throw new ArgumentOutOfRangeException();

        // name generators
        string MemberString(int count, bool capitalize = false)
            => capitalize
                ? count == 1 ? "Member" : "Members"
                : count == 1
                    ? "member"
                    : "members";

        string GroupString(int count)
            => count == 1 ? "group" : "groups";

        // string generators

        string ResponseString()
        {
            if (actionedOn > 0 && notActionedOn > 0 && memberPlural)
                return $"{actionedOn} {MemberString(actionedOn)} {actionStr} {GroupString(groupCount)}";
            if (actionedOn > 0 && notActionedOn > 0 && groupPlural)
                return $"{MemberString(memberCount, true)} {actionStr} {actionedOn} {GroupString(actionedOn)}";
            if (notActionedOn == 0)
                return $"{MemberString(memberCount, true)} {actionStr} {GroupString(groupCount)}";
            if (actionedOn == 0)
                return $"{MemberString(memberCount, true)} not {actionStr} {GroupString(groupCount)}";

            throw new ArgumentOutOfRangeException();
        }

        string InfoMessage()
        {
            if (notActionedOn == 0) return "";

            var msg = "";
            if (actionedOn > 0 && memberPlural)
                msg += $"{notActionedOn} {MemberString(notActionedOn)}";
            else
                msg += $"{MemberString(memberCount)}";

            msg += $" already {containStr}";

            if (actionedOn > 0 && groupPlural)
                msg += $" {notActionedOn} {GroupString(notActionedOn)}";
            else
                msg += $" {GroupString(groupCount)}";

            return $" ({msg})";
        }

        return $"{emojiStr} {ResponseString()}{InfoMessage()}.";
    }
}