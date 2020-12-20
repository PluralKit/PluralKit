using System;
using System.Collections.Generic;

using PluralKit.Core;

namespace PluralKit.API.Models
{
    public static class ApiModelExt
    {
        public static ApiSystem ToApiSystem(this PKSystem sys, LookupContext ctx)
        {
            return new ApiSystem
            {
                SystemId = sys.Uuid,
                ShortId = sys.Hid,
                Name = sys.Name,
                Description = sys.DescriptionFor(ctx),
                Icon = sys.AvatarUrl,
                Tag = sys.Tag,
                Created = sys.Created,
                Config = new ApiSystemConfig {Timezone = sys.UiTz, PingsEnabled = sys.PingsEnabled},
                Privacy = ctx == LookupContext.ByOwner
                    ? new ApiSystemPrivacy
                    {
                        Description = sys.DescriptionPrivacy,
                        GroupList = sys.GroupListPrivacy,
                        LastSwitch = sys.FrontPrivacy,
                        MemberList = sys.MemberListPrivacy,
                        SwitchHistory = sys.FrontHistoryPrivacy
                    }
                    : null
            };
        }

        public static ApiMember ToApiMember(this PKMember mem, LookupContext ctx)
        {
            return new ApiMember
            {
                MemberId = mem.Uuid,
                ShortId = mem.Hid,
                Name = mem.NameFor(ctx),
                DisplayName = mem.NamePrivacy.CanAccess(ctx) ? mem.DisplayName : null,
                Description = mem.DescriptionFor(ctx),
                Avatar = mem.AvatarFor(ctx),
                Pronouns = mem.PronounsFor(ctx),
                Color = mem.Color,
                Birthday = mem.BirthdayFor(ctx),
                MessageCount = mem.MessageCountFor(ctx),
                Privacy = ctx == LookupContext.ByOwner
                    ? new ApiMemberPrivacy
                    {
                        Visibility = mem.MemberVisibility,
                        Name = mem.NamePrivacy,
                        Description = mem.DescriptionPrivacy,
                        Avatar = mem.AvatarPrivacy,
                        Birthday = mem.BirthdayPrivacy,
                        Pronouns = mem.PronounPrivacy,
                        Metadata = mem.MetadataPrivacy
                    }
                    : null,
                Created = mem.CreatedFor(ctx)
            };
        }
        
        public static ApiSwitch ToApiSwitch(this PKSwitch sw, IEnumerable<Guid> members, LookupContext ctx)
        {
            return new ApiSwitch
            {
                SwitchId = sw.Uuid,
                Note = ctx == LookupContext.ByOwner ? sw.Note : null,
                Timestamp = sw.Timestamp,
                Members = members
            };
        }

        public static ApiGroup ToApiGroup(this PKGroup g, LookupContext ctx)
        {
            return new ApiGroup
            {
                GroupId = g.Uuid,
                ShortId = g.Hid,
                Name = g.Name,
                DisplayName = g.DisplayName,
                Description = g.DescriptionFor(ctx),
                Icon = g.IconFor(ctx),
                Created = g.Created,
                Privacy = ctx == LookupContext.ByOwner
                    ? new ApiGroupPrivacy
                    {
                        Description = g.DescriptionPrivacy,
                        Icon = g.IconPrivacy,
                        List = g.ListPrivacy,
                        Visibility = g.Visibility
                    }
                    : null
            };
        }

        public static SystemPatch ToSystemPatch(this ApiSystemPatch patch)
        {
            return new SystemPatch
            {
                Name = patch.Name,
                Description = patch.Description,
                Tag = patch.Tag,
                AvatarUrl = patch.Icon,
                UiTz = patch.Config.Then(p => p.Timezone),
                PingsEnabled = patch.Config.Then(p => p.PingsEnabled),
                DescriptionPrivacy = patch.Privacy.Then(p => p.Description),
                MemberListPrivacy = patch.Privacy.Then(p => p.MemberList),
                GroupListPrivacy = patch.Privacy.Then(p => p.GroupList),
                FrontPrivacy = patch.Privacy.Then(p => p.LastSwitch),
                FrontHistoryPrivacy = patch.Privacy.Then(p => p.SwitchHistory)
            };
        }

        public static MemberPatch ToMemberPatch(this ApiMemberPatch patch)
        {
            return new MemberPatch
            {
                Name = patch.Name,
                DisplayName = patch.DisplayName,
                Description = patch.Description,
                AvatarUrl = patch.Avatar,
                Pronouns = patch.Avatar,
                Color = patch.Color,
                Birthday = patch.Birthday,
                Visibility = patch.Privacy.Then(p => p.Visibility),
                NamePrivacy = patch.Privacy.Then(p => p.Name),
                DescriptionPrivacy = patch.Privacy.Then(p => p.Description),
                AvatarPrivacy = patch.Privacy.Then(p => p.Avatar),
                BirthdayPrivacy = patch.Privacy.Then(p => p.Birthday),
                PronounPrivacy = patch.Privacy.Then(p => p.Pronouns),
                MetadataPrivacy = patch.Privacy.Then(p => p.Metadata)
            };
        }

        public static GroupPatch ToGroupPatch(this ApiGroupPatch patch)
        {
            return new GroupPatch
            {
                Name = patch.Name,
                DisplayName = patch.DisplayName,
                Description = patch.Description,
                Icon = patch.Icon,
                Visibility = patch.Privacy.Then(p => p.Visibility),
                DescriptionPrivacy = patch.Privacy.Then(p => p.Description),
                IconPrivacy = patch.Privacy.Then(p => p.Icon),
                ListPrivacy = patch.Privacy.Then(p => p.List)
            };
        }

        public static SwitchPatch ToSwitchPatch(this ApiSwitchPatch patch)
        {
            return new SwitchPatch
            {
                Timestamp = patch.Timestamp,
                Note = patch.Note
            };
        }
    }
}