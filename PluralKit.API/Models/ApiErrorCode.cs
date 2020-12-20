using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PluralKit.API.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ApiErrorCode
    {
        NotAuthenticated,
        NoPermission,
        InvalidSystemReference,
        InvalidMemberReference,
        InvalidGroupReference,
        SystemNotFound,
        MemberNotFound,
        GroupNotFound,
        SwitchNotFound,
        
        MemberNameRequired,
        GroupNameRequired,
        SwitchMembersRequired,
        InvalidSystemData,
        InvalidMemberData,
        InvalidGroupData,
        InvalidSwitchData,

        SwitchMemberNotInSystem,
        TooManySwitchMembers,
        DuplicateSwitchMember,
        CannotUpdateSwitchMembers,

        MemberLimitReached,
        GroupLimitReached,
    }
}