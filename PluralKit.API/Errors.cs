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
        public ModelParseError() : base(400, 0, "Error parsing JSON model")
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
        public static PKError SwitchNotFound = new(404, 20005, "Switch not found.");
        public static PKError UnauthorizedMemberList = new(403, 30001, "Unauthorized to view member list");
        public static PKError UnauthorizedGroupList = new(403, 30002, "Unauthorized to view group list");
        public static PKError UnauthorizedGroupMemberList = new(403, 30003, "Unauthorized to view group member list");
        public static PKError UnauthorizedCurrentFronters = new(403, 30004, "Unauthorized to view current fronters.");
        public static PKError UnauthorizedFrontHistory = new(403, 30004, "Unauthorized to view front history.");
        public static PKError Unimplemented = new(501, 50001, "Unimplemented");
    }
}