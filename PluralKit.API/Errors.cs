using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

public class PKError: Exception
{
    public PKError(int code, int json_code, string message) : base(message)
    {
        ResponseCode = code;
        JsonCode = json_code;
    }

    public int ResponseCode { get; init; }
    public int JsonCode { get; init; }

    public JObject ToJson()
    {
        var j = new JObject();
        j.Add("message", Message);
        j.Add("code", JsonCode);
        return j;
    }
}

public class ModelParseError: PKError
{
    public ModelParseError(IEnumerable<ValidationError> errors) : base(400, 40001, "Error parsing JSON model")
    {
        _errors = errors;
    }

    private IEnumerable<ValidationError> _errors { get; }

    public new JObject ToJson()
    {
        var j = base.ToJson();
        var e = new JObject();

        foreach (var err in _errors)
        {
            var o = new JObject();

            if (err is FieldTooLongError fe)
            {
                o.Add("message", $"Field {err.Key} is too long.");
                o.Add("actual_length", fe.ActualLength);
                o.Add("max_length", fe.MaxLength);
            }
            else if (err.Text != null)
            {
                o.Add("message", err.Text);
            }
            else
            {
                o.Add("message", $"Field {err.Key} is invalid.");
            }

            if (e[err.Key] == null)
                e.Add(err.Key, new JArray());

            (e[err.Key] as JArray).Add(o);
        }

        j.Add("errors", e);
        return j;
    }
}

public static class Errors
{
    public static PKError GenericBadRequest = new(400, 0, "400: Bad Request");
    public static PKError GenericAuthError = new(401, 0, "401: Missing or invalid Authorization header");
    public static PKError GenericMissingPermissions = new(403, 0, "403: Missing permissions to access this resource");

    public static PKError SystemNotFound = new(404, 20001, "System not found.");
    public static PKError MemberNotFound = new(404, 20002, "Member not found.");
    public static PKError MemberNotFoundWithRef(string memberRef) =>
        new(404, 20003, $"Member '{memberRef}' not found.");
    public static PKError GroupNotFound = new(404, 20004, "Group not found.");
    public static PKError GroupNotFoundWithRef(string groupRef) =>
        new(404, 20005, $"Group '{groupRef}' not found.");
    public static PKError MessageNotFound = new(404, 20006, "Message not found.");
    public static PKError SwitchNotFound = new(404, 20007, "Switch not found.");
    public static PKError SwitchNotFoundPublic = new(404, 20008,
        "Switch not found, switch associated with different system, or unauthorized to view front history.");
    public static PKError SystemGuildNotFound = new(404, 20009, "No system guild settings found for target guild.");
    public static PKError MemberGuildNotFound = new(404, 20010, "No member guild settings found for target guild.");

    public static PKError UnauthorizedMemberList = new(403, 30001, "Unauthorized to view member list");
    public static PKError UnauthorizedGroupList = new(403, 30002, "Unauthorized to view group list");
    public static PKError UnauthorizedGroupMemberList = new(403, 30003, "Unauthorized to view group member list");
    public static PKError UnauthorizedCurrentFronters = new(403, 30004, "Unauthorized to view current fronters.");
    public static PKError UnauthorizedFrontHistory = new(403, 30005, "Unauthorized to view front history.");
    public static PKError NotOwnMemberError = new(403, 30006, "Target member is not part of your system.");
    public static PKError NotOwnGroupError = new(403, 30007, "Target group is not part of your system.");
    // todo: somehow add the memberRef to the JSON
    public static PKError NotOwnMemberErrorWithRef(string memberRef) =>
        new(403, 30008, $"Member '{memberRef}' is not part of your system.");
    public static PKError NotOwnGroupErrorWithRef(string groupRef) =>
        new(403, 30009, $"Group '{groupRef}' is not part of your system.");

    public static PKError MissingAutoproxyMember =
        new(400, 40002, "Missing autoproxy member for member-mode autoproxy.");
    public static PKError DuplicateMembersInList = new(400, 40003, "Duplicate members in member list.");
    public static PKError SameSwitchMembersError =
        new(400, 40004, "Member list identical to current fronter list.");
    public static PKError SameSwitchTimestampError =
        new(400, 40005, "Switch with provided timestamp already exists.");
    public static PKError InvalidSwitchId = new(400, 40006, "Invalid switch ID.");
    public static PKError MemberLimitReached = new(400, 40007, "Member limit reached.");
    public static PKError GroupLimitReached = new(400, 40008, "Group limit reached.");
    public static PKError PatchLatchMemberError = new(400, 40009, "Cannot patch autoproxy member with latch-mode autoproxy.");
    public static PKError Unimplemented = new(501, 50001, "Unimplemented");
}

public static class APIErrorHandlerExt
{
    public static bool IsUserError(this Exception exc)
    {
        // caused by users sending an incorrect JSON type (array where an object is expected, etc)
        if (exc is InvalidCastException && exc.Message.Contains("Newtonsoft.Json"))
            return true;

        // Hacky parsing of timestamps results in hacky error handling. Probably fix this one at some point.
        if (exc is FormatException && exc.Message.Contains("was not recognized as a valid DateTime"))
            return true;

        // this happens if a user sends an empty JSON object for PATCH (or a JSON object with no valid keys)
        if (exc is InvalidPatchException)
            return true;

        // This may expanded at some point.
        return false;
    }
}