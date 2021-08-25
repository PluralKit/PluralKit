using System;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public static class GroupAddRemoveResponseService
    {
        public static string GenerateResponse(Groups.AddRemoveOperation action, int memberCount, int groupCount, int actionedOn, int notActionedOn)
            => new Response(action, memberCount, groupCount, actionedOn, notActionedOn).ToString();

        private class Response
        {
            private readonly Groups.AddRemoveOperation _op;
            
            private readonly string _actionStr;
            private readonly string _containStr;
            private readonly string _emojiStr;

            private readonly bool _memberPlural;
            private readonly bool _groupPlural;
            
            private readonly int _actionedOn;
            private readonly int _notActionedOn;

            public Response(Groups.AddRemoveOperation action, int memberCount, int groupCount, int actionedOn,
                            int notActionedOn)
            {
                _op = action;
                
                _actionStr = action == Groups.AddRemoveOperation.Add ? "added to" : "removed from";
                _containStr = action == Groups.AddRemoveOperation.Add ? "in" : "not in";
                _emojiStr = actionedOn > 0 ? Emojis.Success : Emojis.Error;

                _memberPlural = memberCount > 1;
                _groupPlural = groupCount > 1;
                
                // sanity checking: we can't add multiple groups to multiple members (at least for now)
                if (_memberPlural && _groupPlural)
                    throw new ArgumentOutOfRangeException();
               
                // sanity checking: we can't act/not act on a different number of entities than we have
                if (_memberPlural && (actionedOn + notActionedOn) != memberCount)
                    throw new ArgumentOutOfRangeException();
                if (_groupPlural && (actionedOn + notActionedOn) != groupCount)
                    throw new ArgumentOutOfRangeException();

                _actionedOn = actionedOn;
                _notActionedOn = notActionedOn;
            }
            
            // name generators
            private string MemberString(bool capitalize = false)
                => capitalize
                    ? (_memberPlural ? "Members" : "Member")
                    : (_memberPlural ? "members" : "member");

            private string MemberString(int count, bool capitalize = false)
                => capitalize
                    ? (count == 1 ? "Member" : "Members")
                    : (count == 1 ? "member" : "members");
            
            private string GroupString() => _groupPlural ? "groups" : "group";

            private string GroupString(int count)
                => count == 1 ? "group" : "groups";

            // string generators
            
            private string ResponseString()
            {
                if (_actionedOn > 0 && _notActionedOn > 0 && _memberPlural)
                    return $"{_actionedOn} {MemberString(_actionedOn)} {_actionStr} {GroupString()}";
                if (_actionedOn > 0 && _notActionedOn > 0 && _groupPlural)
                    return $"{MemberString(capitalize: true)} {_actionStr} {_actionedOn} {GroupString(_actionedOn)}";
                if (_notActionedOn == 0)
                    return $"{MemberString(capitalize: true)} {_actionStr} {GroupString()}";
                if (_actionedOn == 0)
                    return $"{MemberString(capitalize: true)} not {_actionStr} {GroupString()}";

                throw new ArgumentOutOfRangeException();
            }

            private string InfoMessage()
            {
                if (_notActionedOn == 0) return $"";

                var msg = "";
                if (_actionedOn > 0 && _memberPlural)
                    msg += $"{_notActionedOn} {MemberString(_notActionedOn)}";
                else
                    msg += $"{MemberString()}";

                msg += $" already {_containStr}";

                if (_actionedOn > 0 && _groupPlural)
                    msg += $" {_notActionedOn} {GroupString(_notActionedOn)}";
                else
                    msg += $" {GroupString()}";

                return $" ({msg})";
            }
            
            public string ToString() => $"{_emojiStr} {ResponseString()}{InfoMessage()}.";
            
            // |
        }
    }
}