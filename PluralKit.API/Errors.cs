using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

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
        private IEnumerable<ValidationError> _errors { get; init; }
        public ModelParseError(IEnumerable<ValidationError> errors) : base(400, 40001, "Error parsing JSON model")
        {
            _errors = errors;
        }

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
                    o.Add("message", err.Text);
                else
                    o.Add("message", $"Field {err.Key} is invalid.");

                if (e[err.Key] != null)
                {
                    if (e[err.Key].Type == JTokenType.Object)
                    {
                        var current = e[err.Key];
                        e.Remove(err.Key);
                        e.Add(err.Key, new JArray());
                        (e[err.Key] as JArray).Add(current);
                    }

                    (e[err.Key] as JArray).Add(o);
                }
                else
                    e.Add(err.Key, o);
            }

            j.Add("errors", e);
            return j;
        }
    }

    public static class Errors
    {
        public static PKError GenericBadRequest = new(400, 0, "400: Bad Request");
        public static PKError GenericAuthError = new(401, 0, "401: Missing or invalid Authorization header");
        public static PKError SystemNotFound = new(404, 20001, "System not found.");
        public static PKError MemberNotFound = new(404, 20002, "Member not found.");
        public static PKError GroupNotFound = new(404, 20003, "Group not found.");
        public static PKError MessageNotFound = new(404, 20004, "Message not found.");
        public static PKError SwitchNotFound = new(404, 20005, "Switch not found.");
        public static PKError SwitchNotFoundPublic = new(404, 20005, "Switch not found, switch associated with different system, or unauthorized to view front history.");
        public static PKError SystemGuildNotFound = new(404, 20006, "No system guild settings found for target guild.");
        public static PKError MemberGuildNotFound = new(404, 20007, "No member guild settings found for target guild.");
        public static PKError UnauthorizedMemberList = new(403, 30001, "Unauthorized to view member list");
        public static PKError UnauthorizedGroupList = new(403, 30002, "Unauthorized to view group list");
        public static PKError UnauthorizedGroupMemberList = new(403, 30003, "Unauthorized to view group member list");
        public static PKError UnauthorizedCurrentFronters = new(403, 30004, "Unauthorized to view current fronters.");
        public static PKError UnauthorizedFrontHistory = new(403, 30005, "Unauthorized to view front history.");
        public static PKError NotOwnMemberError = new(403, 30006, "Target member is not part of your system.");
        public static PKError NotOwnGroupError = new(403, 30007, "Target group is not part of your system.");
        // todo: somehow add the memberRef to the JSON
        public static PKError NotOwnMemberErrorWithRef(string memberRef) => new(403, 30008, $"Member '{memberRef}' is not part of your system.");
        public static PKError NotOwnGroupErrorWithRef(string groupRef) => new(403, 30009, $"Group '{groupRef}' is not part of your system.");
        public static PKError MissingAutoproxyMember = new(400, 40002, "Missing autoproxy member for member-mode autoproxy.");
        public static PKError DuplicateMembersInList = new(400, 40003, "Duplicate members in member list.");
        public static PKError SameSwitchMembersError = new(400, 40004, "Member list identical to current fronter list.");
        public static PKError SameSwitchTimestampError = new(400, 40005, "Switch with provided timestamp already exists.");
        public static PKError InvalidSwitchId = new(400, 40006, "Invalid switch ID.");
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

            // This may expanded at some point.
            return false;
        }
    }
}