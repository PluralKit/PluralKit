using System;

using Newtonsoft.Json.Linq;

namespace PluralKit.API
{
    public class PKError: Exception
    {
        public int ResponseCode { get; init; }
        public int JsonCode { get; init; }
        public PKError(int code, int json_code, string message) : base(message)
        {
            ResponseCode = code;
            JsonCode = json_code;
        }

        public JObject ToJson()
        {
            var j = new JObject();
            j.Add("message", this.Message);
            j.Add("code", this.JsonCode);
            return j;
        }
    }

    public class ModelParseError: PKError
    {
        public ModelParseError() : base(400, 40001, "Error parsing JSON model")
        {
            // todo
        }

        public new JObject ToJson()
        {
            var j = base.ToJson();

            return j;
        }
    }

    public static class APIErrors
    {
        public static PKError GenericBadRequest = new(400, 0, "400: Bad Request");
        public static PKError GenericAuthError = new(401, 0, "401: Missing or invalid Authorization header");
        public static PKError SystemNotFound = new(404, 20001, "System not found.");
        public static PKError MemberNotFound = new(404, 20002, "Member not found.");
        public static PKError GroupNotFound = new(404, 20003, "Group not found.");
        public static PKError MessageNotFound = new(404, 20004, "Message not found.");
        public static PKError SwitchNotFound = new(404, 20005, "Switch not found, switch is associated to different system, or unauthorized to view front history.");
        public static PKError SystemGuildNotFound = new(404, 20006, "No system guild settings found for target guild.");
        public static PKError MemberGuildNotFound = new(404, 20007, "No member guild settings found for target guild.");
        public static PKError UnauthorizedMemberList = new(403, 30001, "Unauthorized to view member list");
        public static PKError UnauthorizedGroupList = new(403, 30002, "Unauthorized to view group list");
        public static PKError UnauthorizedGroupMemberList = new(403, 30003, "Unauthorized to view group member list");
        public static PKError UnauthorizedCurrentFronters = new(403, 30004, "Unauthorized to view current fronters.");
        public static PKError UnauthorizedFrontHistory = new(403, 30005, "Unauthorized to view front history.");
        public static PKError NotOwnMemberError = new(403, 30006, "Target member is not part of your system.");
        public static PKError NotOwnGroupError = new(403, 30006, "Target group is not part of your system.");
        // todo: somehow add the memberRef to the JSON
        public static PKError NotOwnMemberErrorWithRef(string memberRef) => new(403, 30008, $"Member '{memberRef}' is not part of your system.");
        public static PKError NotOwnGroupErrorWithRef(string groupRef) => new(403, 30009, $"Group '{groupRef}' is not part of your system.");
        public static PKError MissingAutoproxyMember = new(400, 40002, "Missing autoproxy member for member-mode autoproxy.");
        public static PKError Unimplemented = new(501, 50001, "Unimplemented");
    }
}