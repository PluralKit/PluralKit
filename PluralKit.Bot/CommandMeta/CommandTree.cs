using PluralKit.Core;

namespace PluralKit.Bot;

public partial class CommandTree
{
    public Task ExecuteCommand(Context ctx, Commands command)
    {
        return command switch
        {
            Commands.Help(_, var flags) => ctx.Execute<Help>(Help, m => m.HelpRoot(ctx, flags.show_embed)),
            Commands.HelpCommands => ctx.Reply(
                    "For the list of commands, see the website: <https://pluralkit.me/commands>"),
            Commands.HelpProxy => ctx.Reply(
                    "The proxy help page has been moved! See the website: https://pluralkit.me/guide#proxying"),
            Commands.MemberShow(var param, var flags) => ctx.Execute<Member>(MemberInfo, m => m.ViewMember(ctx, param.target, flags.show_embed)),
            Commands.MemberNew(var param, _) => ctx.Execute<Member>(MemberNew, m => m.NewMember(ctx, param.name)),
            Commands.MemberSoulscream(var param, _) => ctx.Execute<Member>(MemberInfo, m => m.Soulscream(ctx, param.target)),
            Commands.MemberAvatarShow(var param, var flags) => ctx.Execute<MemberAvatar>(MemberAvatar, m => m.ShowAvatar(ctx, param.target, flags.GetReplyFormat())),
            Commands.MemberAvatarClear(var param, var flags) => ctx.Execute<MemberAvatar>(MemberAvatar, m => m.ClearAvatar(ctx, param.target)),
            Commands.MemberAvatarUpdate(var param, _) => ctx.Execute<MemberAvatar>(MemberAvatar, m => m.ChangeAvatar(ctx, param.target, param.avatar)),
            Commands.MemberWebhookAvatarShow(var param, var flags) => ctx.Execute<MemberAvatar>(MemberAvatar, m => m.ShowWebhookAvatar(ctx, param.target, flags.GetReplyFormat())),
            Commands.MemberWebhookAvatarClear(var param, var flags) => ctx.Execute<MemberAvatar>(MemberAvatar, m => m.ClearWebhookAvatar(ctx, param.target)),
            Commands.MemberWebhookAvatarUpdate(var param, _) => ctx.Execute<MemberAvatar>(MemberAvatar, m => m.ChangeWebhookAvatar(ctx, param.target, param.avatar)),
            Commands.MemberServerAvatarShow(var param, var flags) => ctx.Execute<MemberAvatar>(MemberAvatar, m => m.ShowServerAvatar(ctx, param.target, flags.GetReplyFormat())),
            Commands.MemberServerAvatarClear(var param, var flags) => ctx.Execute<MemberAvatar>(MemberAvatar, m => m.ClearServerAvatar(ctx, param.target)),
            Commands.MemberServerAvatarUpdate(var param, _) => ctx.Execute<MemberAvatar>(MemberAvatar, m => m.ChangeServerAvatar(ctx, param.target, param.avatar)),
            Commands.MemberPronounsShow(var param, var flags) => ctx.Execute<MemberEdit>(MemberPronouns, m => m.ShowPronouns(ctx, param.target, flags.GetReplyFormat())),
            Commands.MemberPronounsClear(var param, var flags) => ctx.Execute<MemberEdit>(MemberPronouns, m => m.ClearPronouns(ctx, param.target, flags.yes)),
            Commands.MemberPronounsUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberPronouns, m => m.ChangePronouns(ctx, param.target, param.pronouns)),
            Commands.MemberDescShow(var param, var flags) => ctx.Execute<MemberEdit>(MemberDesc, m => m.ShowDescription(ctx, param.target, flags.GetReplyFormat())),
            Commands.MemberDescClear(var param, var flags) => ctx.Execute<MemberEdit>(MemberDesc, m => m.ClearDescription(ctx, param.target, flags.yes)),
            Commands.MemberDescUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberDesc, m => m.ChangeDescription(ctx, param.target, param.description)),
            Commands.MemberNameShow(var param, var flags) => ctx.Execute<MemberEdit>(MemberInfo, m => m.ShowName(ctx, param.target, flags.GetReplyFormat())),
            Commands.MemberNameUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberInfo, m => m.ChangeName(ctx, param.target, param.name)),
            Commands.MemberBannerShow(var param, var flags) => ctx.Execute<MemberEdit>(MemberBannerImage, m => m.ShowBannerImage(ctx, param.target, flags.GetReplyFormat())),
            Commands.MemberBannerClear(var param, var flags) => ctx.Execute<MemberEdit>(MemberBannerImage, m => m.ClearBannerImage(ctx, param.target, flags.yes)),
            Commands.MemberBannerUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberBannerImage, m => m.ChangeBannerImage(ctx, param.target, param.banner)),
            Commands.MemberColorShow(var param, var flags) => ctx.Execute<MemberEdit>(MemberColor, m => m.ShowColor(ctx, param.target, flags.GetReplyFormat())),
            Commands.MemberColorClear(var param, var flags) => ctx.Execute<MemberEdit>(MemberColor, m => m.ClearColor(ctx, param.target, flags.yes)),
            Commands.MemberColorUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberColor, m => m.ChangeColor(ctx, param.target, param.color)),
            Commands.MemberBirthdayShow(var param, var flags) => ctx.Execute<MemberEdit>(MemberBirthday, m => m.ShowBirthday(ctx, param.target, flags.GetReplyFormat())),
            Commands.MemberBirthdayClear(var param, var flags) => ctx.Execute<MemberEdit>(MemberBirthday, m => m.ClearBirthday(ctx, param.target, flags.yes)),
            Commands.MemberBirthdayUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberBirthday, m => m.ChangeBirthday(ctx, param.target, param.birthday)),
            Commands.MemberDisplaynameShow(var param, var flags) => ctx.Execute<MemberEdit>(MemberDisplayName, m => m.ShowDisplayName(ctx, param.target, flags.GetReplyFormat())),
            Commands.MemberDisplaynameClear(var param, var flags) => ctx.Execute<MemberEdit>(MemberDisplayName, m => m.ClearDisplayName(ctx, param.target, flags.yes)),
            Commands.MemberDisplaynameUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberDisplayName, m => m.ChangeDisplayName(ctx, param.target, param.name)),
            Commands.MemberServernameShow(var param, var flags) => ctx.Execute<MemberEdit>(MemberServerName, m => m.ShowServerName(ctx, param.target, flags.GetReplyFormat())),
            Commands.MemberServernameClear(var param, var flags) => ctx.Execute<MemberEdit>(MemberServerName, m => m.ClearServerName(ctx, param.target, flags.yes)),
            Commands.MemberServernameUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberServerName, m => m.ChangeServerName(ctx, param.target, param.name)),
            Commands.MemberKeepproxyShow(var param, _) => ctx.Execute<MemberEdit>(MemberKeepProxy, m => m.ShowKeepProxy(ctx, param.target)),
            Commands.MemberKeepproxyUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberKeepProxy, m => m.ChangeKeepProxy(ctx, param.target, param.value)),
            Commands.MemberServerKeepproxyShow(var param, _) => ctx.Execute<MemberEdit>(MemberServerKeepProxy, m => m.ShowServerKeepProxy(ctx, param.target)),
            Commands.MemberServerKeepproxyUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberServerKeepProxy, m => m.ChangeServerKeepProxy(ctx, param.target, param.value)),
            Commands.MemberServerKeepproxyClear(var param, var flags) => ctx.Execute<MemberEdit>(MemberServerKeepProxy, m => m.ClearServerKeepProxy(ctx, param.target, flags.yes)),
            Commands.MemberProxyShow(var param, _) => ctx.Execute<MemberProxy>(MemberProxy, m => m.ShowProxy(ctx, param.target)),
            Commands.MemberProxyClear(var param, var flags) => ctx.Execute<MemberProxy>(MemberProxy, m => m.ClearProxy(ctx, param.target)),
            Commands.MemberProxyAdd(var param, _) => ctx.Execute<MemberProxy>(MemberProxy, m => m.AddProxy(ctx, param.target, param.tag)),
            Commands.MemberProxyRemove(var param, _) => ctx.Execute<MemberProxy>(MemberProxy, m => m.RemoveProxy(ctx, param.target, param.tag)),
            Commands.MemberProxySet(var param, _) => ctx.Execute<MemberProxy>(MemberProxy, m => m.SetProxy(ctx, param.target, param.tags)),
            Commands.MemberTtsShow(var param, _) => ctx.Execute<MemberEdit>(MemberTts, m => m.ShowTts(ctx, param.target)),
            Commands.MemberTtsUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberTts, m => m.ChangeTts(ctx, param.target, param.value)),
            Commands.MemberAutoproxyShow(var param, _) => ctx.Execute<MemberEdit>(MemberAutoproxy, m => m.ShowAutoproxy(ctx, param.target)),
            Commands.MemberAutoproxyUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberAutoproxy, m => m.ChangeAutoproxy(ctx, param.target, param.value)),
            Commands.MemberDelete(var param, _) => ctx.Execute<MemberEdit>(MemberDelete, m => m.Delete(ctx, param.target)),
            Commands.MemberPrivacyShow(var param, _) => ctx.Execute<MemberEdit>(MemberPrivacy, m => m.ShowPrivacy(ctx, param.target)),
            Commands.MemberPrivacyUpdate(var param, _) => ctx.Execute<MemberEdit>(MemberPrivacy, m => m.ChangePrivacy(ctx, param.target, param.member_privacy_target, param.new_privacy_level)),
            Commands.MemberGroupAdd(var param, _) => ctx.Execute<GroupMember>(MemberGroupAdd, m => m.AddRemoveGroups(ctx, param.target, param.groups, Groups.AddRemoveOperation.Add)),
            Commands.MemberGroupRemove(var param, _) => ctx.Execute<GroupMember>(MemberGroupRemove, m => m.AddRemoveGroups(ctx, param.target, param.groups, Groups.AddRemoveOperation.Remove)),
            Commands.MemberId(var param, _) => ctx.Execute<Member>(MemberId, m => m.DisplayId(ctx, param.target)),
            Commands.CfgApAccountShow => ctx.Execute<Config>(null, m => m.ViewAutoproxyAccount(ctx)),
            Commands.CfgApAccountUpdate(var param, _) => ctx.Execute<Config>(null, m => m.EditAutoproxyAccount(ctx, param.toggle)),
            Commands.CfgApTimeoutShow => ctx.Execute<Config>(null, m => m.ViewAutoproxyTimeout(ctx)),
            Commands.CfgApTimeoutOff => ctx.Execute<Config>(null, m => m.DisableAutoproxyTimeout(ctx)),
            Commands.CfgApTimeoutReset => ctx.Execute<Config>(null, m => m.ResetAutoproxyTimeout(ctx)),
            Commands.CfgApTimeoutUpdate(var param, _) => ctx.Execute<Config>(null, m => m.EditAutoproxyTimeout(ctx, param.timeout)),
            Commands.FunThunder => ctx.Execute<Fun>(null, m => m.Thunder(ctx)),
            Commands.FunMeow => ctx.Execute<Fun>(null, m => m.Meow(ctx)),
            Commands.SystemInfo(var param, var flags) => ctx.Execute<System>(SystemInfo, m => m.Query(ctx, param.target, flags.all, flags.@public, flags.@private)),
            Commands.SystemInfoSelf(_, var flags) => ctx.Execute<System>(SystemInfo, m => m.Query(ctx, ctx.System, flags.all, flags.@public, flags.@private)),
            Commands.SystemNew(var param, _) => ctx.Execute<System>(SystemNew, m => m.New(ctx, null)),
            Commands.SystemNewName(var param, _) => ctx.Execute<System>(SystemNew, m => m.New(ctx, param.name)),
            Commands.SystemShowNameSelf(_, var flags) => ctx.Execute<SystemEdit>(SystemRename, m => m.ShowName(ctx, ctx.System, flags.GetReplyFormat())),
            Commands.SystemShowName(var param, var flags) => ctx.Execute<SystemEdit>(SystemRename, m => m.ShowName(ctx, param.target, flags.GetReplyFormat())),
            Commands.SystemRename(var param, _) => ctx.Execute<SystemEdit>(SystemRename, m => m.Rename(ctx, ctx.System, param.name)),
            Commands.SystemClearName(var param, var flags) => ctx.Execute<SystemEdit>(SystemRename, m => m.ClearName(ctx, ctx.System, flags.yes)),
            Commands.SystemShowServerNameSelf(_, var flags) => ctx.Execute<SystemEdit>(SystemServerName, m => m.ShowServerName(ctx, ctx.System, flags.GetReplyFormat())),
            Commands.SystemShowServerName(var param, var flags) => ctx.Execute<SystemEdit>(SystemServerName, m => m.ShowServerName(ctx, param.target, flags.GetReplyFormat())),
            Commands.SystemClearServerName(var param, var flags) => ctx.Execute<SystemEdit>(SystemServerName, m => m.ClearServerName(ctx, ctx.System, flags.yes)),
            Commands.SystemRenameServerName(var param, _) => ctx.Execute<SystemEdit>(SystemServerName, m => m.RenameServerName(ctx, ctx.System, param.name)),
            Commands.SystemShowDescriptionSelf(_, var flags) => ctx.Execute<SystemEdit>(SystemDesc, m => m.ShowDescription(ctx, ctx.System, flags.GetReplyFormat())),
            Commands.SystemShowDescription(var param, var flags) => ctx.Execute<SystemEdit>(SystemDesc, m => m.ShowDescription(ctx, param.target, flags.GetReplyFormat())),
            Commands.SystemClearDescription(var param, var flags) => ctx.Execute<SystemEdit>(SystemDesc, m => m.ClearDescription(ctx, ctx.System, flags.yes)),
            Commands.SystemChangeDescription(var param, _) => ctx.Execute<SystemEdit>(SystemDesc, m => m.ChangeDescription(ctx, ctx.System, param.description)),
            Commands.SystemShowColorSelf(_, var flags) => ctx.Execute<SystemEdit>(SystemColor, m => m.ShowColor(ctx, ctx.System, flags.GetReplyFormat())),
            Commands.SystemShowColor(var param, var flags) => ctx.Execute<SystemEdit>(SystemColor, m => m.ShowColor(ctx, param.target, flags.GetReplyFormat())),
            Commands.SystemClearColor(var param, var flags) => ctx.Execute<SystemEdit>(SystemColor, m => m.ClearColor(ctx, ctx.System, flags.yes)),
            Commands.SystemChangeColor(var param, _) => ctx.Execute<SystemEdit>(SystemColor, m => m.ChangeColor(ctx, ctx.System, param.color)),
            Commands.SystemShowTagSelf(_, var flags) => ctx.Execute<SystemEdit>(SystemTag, m => m.ShowTag(ctx, ctx.System, flags.GetReplyFormat())),
            Commands.SystemShowTag(var param, var flags) => ctx.Execute<SystemEdit>(SystemTag, m => m.ShowTag(ctx, param.target, flags.GetReplyFormat())),
            Commands.SystemClearTag(var param, var flags) => ctx.Execute<SystemEdit>(SystemTag, m => m.ClearTag(ctx, ctx.System, flags.yes)),
            Commands.SystemChangeTag(var param, _) => ctx.Execute<SystemEdit>(SystemTag, m => m.ChangeTag(ctx, ctx.System, param.tag)),
            Commands.SystemShowServerTagSelf(_, var flags) => ctx.Execute<SystemEdit>(SystemServerTag, m => m.ShowServerTag(ctx, ctx.System, flags.GetReplyFormat())),
            Commands.SystemShowServerTag(var param, var flags) => ctx.Execute<SystemEdit>(SystemServerTag, m => m.ShowServerTag(ctx, param.target, flags.GetReplyFormat())),
            Commands.SystemClearServerTag(var param, var flags) => ctx.Execute<SystemEdit>(SystemServerTag, m => m.ClearServerTag(ctx, ctx.System, flags.yes)),
            Commands.SystemChangeServerTag(var param, _) => ctx.Execute<SystemEdit>(SystemServerTag, m => m.ChangeServerTag(ctx, ctx.System, param.tag)),
            Commands.SystemShowPronounsSelf(_, var flags) => ctx.Execute<SystemEdit>(SystemPronouns, m => m.ShowPronouns(ctx, ctx.System, flags.GetReplyFormat())),
            Commands.SystemShowPronouns(var param, var flags) => ctx.Execute<SystemEdit>(SystemPronouns, m => m.ShowPronouns(ctx, param.target, flags.GetReplyFormat())),
            Commands.SystemClearPronouns(var param, var flags) => ctx.Execute<SystemEdit>(SystemPronouns, m => m.ClearPronouns(ctx, ctx.System, flags.yes)),
            Commands.SystemChangePronouns(var param, _) => ctx.Execute<SystemEdit>(SystemPronouns, m => m.ChangePronouns(ctx, ctx.System, param.pronouns)),
            Commands.SystemShowAvatarSelf(_, var flags) => ((Func<Task>)(() =>
            {
                // we want to change avatar if an attached image is passed
                // we can't have a separate parsed command for this since the parser can't be aware of any attachments
                var attachedImage = ctx.ExtractImageFromAttachment();
                if (attachedImage is { } image)
                    return ctx.Execute<SystemEdit>(SystemAvatar, m => m.ChangeAvatar(ctx, ctx.System, image));
                // if no attachment show the avatar like intended
                return ctx.Execute<SystemEdit>(SystemAvatar, m => m.ShowAvatar(ctx, ctx.System, flags.GetReplyFormat()));
            }))(),
            Commands.SystemShowAvatar(var param, var flags) => ctx.Execute<SystemEdit>(SystemAvatar, m => m.ShowAvatar(ctx, param.target, flags.GetReplyFormat())),
            Commands.SystemClearAvatar(var param, var flags) => ctx.Execute<SystemEdit>(SystemAvatar, m => m.ClearAvatar(ctx, ctx.System, flags.yes)),
            Commands.SystemChangeAvatar(var param, _) => ctx.Execute<SystemEdit>(SystemAvatar, m => m.ChangeAvatar(ctx, ctx.System, param.avatar)),
            Commands.SystemShowServerAvatarSelf(_, var flags) => ((Func<Task>)(() =>
            {
                // we want to change avatar if an attached image is passed
                // we can't have a separate parsed command for this since the parser can't be aware of any attachments
                var attachedImage = ctx.ExtractImageFromAttachment();
                if (attachedImage is { } image)
                    return ctx.Execute<SystemEdit>(SystemServerAvatar, m => m.ChangeServerAvatar(ctx, ctx.System, image));
                // if no attachment show the avatar like intended
                return ctx.Execute<SystemEdit>(SystemServerAvatar, m => m.ShowServerAvatar(ctx, ctx.System, flags.GetReplyFormat()));
            }))(),
            Commands.SystemShowServerAvatar(var param, var flags) => ctx.Execute<SystemEdit>(SystemServerAvatar, m => m.ShowServerAvatar(ctx, param.target, flags.GetReplyFormat())),
            Commands.SystemClearServerAvatar(var param, var flags) => ctx.Execute<SystemEdit>(SystemServerAvatar, m => m.ClearServerAvatar(ctx, ctx.System, flags.yes)),
            Commands.SystemChangeServerAvatar(var param, _) => ctx.Execute<SystemEdit>(SystemServerAvatar, m => m.ChangeServerAvatar(ctx, ctx.System, param.avatar)),
            Commands.SystemShowBannerSelf(_, var flags) => ((Func<Task>)(() =>
            {
                // we want to change banner if an attached image is passed
                // we can't have a separate parsed command for this since the parser can't be aware of any attachments
                var attachedImage = ctx.ExtractImageFromAttachment();
                if (attachedImage is { } image)
                    return ctx.Execute<SystemEdit>(SystemBannerImage, m => m.ChangeBannerImage(ctx, ctx.System, image));
                // if no attachment show the banner like intended
                return ctx.Execute<SystemEdit>(SystemBannerImage, m => m.ShowBannerImage(ctx, ctx.System, flags.GetReplyFormat()));
            }))(),
            Commands.SystemShowBanner(var param, var flags) => ctx.Execute<SystemEdit>(SystemBannerImage, m => m.ShowBannerImage(ctx, param.target, flags.GetReplyFormat())),
            Commands.SystemClearBanner(var param, var flags) => ctx.Execute<SystemEdit>(SystemBannerImage, m => m.ClearBannerImage(ctx, ctx.System, flags.yes)),
            Commands.SystemChangeBanner(var param, _) => ctx.Execute<SystemEdit>(SystemBannerImage, m => m.ChangeBannerImage(ctx, ctx.System, param.banner)),
            Commands.SystemDelete(_, var flags) => ctx.Execute<SystemEdit>(SystemDelete, m => m.Delete(ctx, ctx.System, flags.no_export)),
            Commands.SystemShowProxyCurrent(_, _) => ctx.Execute<SystemEdit>(SystemProxy, m => m.ShowSystemProxy(ctx, ctx.Guild)),
            Commands.SystemShowProxy(var param, _) => ctx.Execute<SystemEdit>(SystemProxy, m => m.ShowSystemProxy(ctx, param.target)),
            Commands.SystemToggleProxyCurrent(var param, _) => ctx.Execute<SystemEdit>(SystemProxy, m => m.ToggleSystemProxy(ctx, ctx.Guild, param.toggle)),
            Commands.SystemToggleProxy(var param, _) => ctx.Execute<SystemEdit>(SystemProxy, m => m.ToggleSystemProxy(ctx, param.target, param.toggle)),
            Commands.SystemShowPrivacy(var param, _) => ctx.Execute<SystemEdit>(SystemPrivacy, m => m.ShowSystemPrivacy(ctx, ctx.System)),
            Commands.SystemChangePrivacyAll(var param, _) => ctx.Execute<SystemEdit>(SystemPrivacy, m => m.ChangeSystemPrivacyAll(ctx, ctx.System, param.level)),
            Commands.SystemChangePrivacy(var param, _) => ctx.Execute<SystemEdit>(SystemPrivacy, m => m.ChangeSystemPrivacy(ctx, ctx.System, param.privacy, param.level)),
            Commands.SwitchOut(_, _) => ctx.Execute<Switch>(SwitchOut, m => m.SwitchOut(ctx)),
            Commands.SwitchDo(var param, _) => ctx.Execute<Switch>(Switch, m => m.SwitchDo(ctx, param.targets)),
            Commands.SwitchMove(var param, _) => ctx.Execute<Switch>(SwitchMove, m => m.SwitchMove(ctx, param.@string)),
            Commands.SwitchEdit(var param, var flags) => ctx.Execute<Switch>(SwitchEdit, m => m.SwitchEdit(ctx, param.targets, false, flags.first, flags.remove, flags.append, flags.prepend)),
            Commands.SwitchEditOut(_, _) => ctx.Execute<Switch>(SwitchEditOut, m => m.SwitchEditOut(ctx)),
            Commands.SwitchDelete(var param, var flags) => ctx.Execute<Switch>(SwitchDelete, m => m.SwitchDelete(ctx, flags.all)),
            Commands.SwitchCopy(var param, var flags) => ctx.Execute<Switch>(SwitchCopy, m => m.SwitchEdit(ctx, param.targets, true, flags.first, flags.remove, flags.append, flags.prepend)),
            Commands.SystemFronter(var param, var flags) => ctx.Execute<SystemFront>(SystemFronter, m => m.Fronter(ctx, param.target)),
            Commands.SystemFronterHistory(var param, var flags) => ctx.Execute<SystemFront>(SystemFrontHistory, m => m.FrontHistory(ctx, param.target, flags.clear)),
            Commands.SystemFronterPercent(var param, var flags) => ctx.Execute<SystemFront>(SystemFrontPercent, m => m.FrontPercent(ctx, param.target, flags.duration, flags.fronters_only, flags.flat)),
            Commands.RandomSelf(_, var flags) =>
            flags.group
                ? ctx.Execute<Random>(GroupRandom, m => m.Group(ctx, ctx.System, flags.all, flags.show_embed))
                : ctx.Execute<Random>(MemberRandom, m => m.Member(ctx, ctx.System, flags.all, flags.show_embed)),
            Commands.SystemRandom(var param, var flags) =>
            flags.group
                ? ctx.Execute<Random>(GroupRandom, m => m.Group(ctx, param.target, flags.all, flags.show_embed))
                : ctx.Execute<Random>(MemberRandom, m => m.Member(ctx, param.target, flags.all, flags.show_embed)),
            Commands.GroupRandomMember(var param, var flags) => ctx.Execute<Random>(GroupMemberRandom, m => m.GroupMember(ctx, param.target, flags)),
            Commands.SystemLink => ctx.Execute<SystemLink>(Link, m => m.LinkSystem(ctx)),
            Commands.SystemUnlink(var param, _) => ctx.Execute<SystemLink>(Unlink, m => m.UnlinkAccount(ctx, param.target)),
            Commands.SystemMembersListSelf(var param, var flags) => ctx.Execute<SystemList>(SystemList, m => m.MemberList(ctx, ctx.System, null, flags)),
            Commands.SystemMembersSearchSelf(var param, var flags) => ctx.Execute<SystemList>(SystemFind, m => m.MemberList(ctx, ctx.System, param.query, flags)),
            Commands.SystemMembersList(var param, var flags) => ctx.Execute<SystemList>(SystemList, m => m.MemberList(ctx, param.target, null, flags)),
            Commands.SystemMembersSearch(var param, var flags) => ctx.Execute<SystemList>(SystemFind, m => m.MemberList(ctx, param.target, param.query, flags)),
            Commands.MemberListGroups(var param, var flags) => ctx.Execute<GroupMember>(MemberGroups, m => m.ListMemberGroups(ctx, param.target, null, flags)),
            Commands.MemberSearchGroups(var param, var flags) => ctx.Execute<GroupMember>(MemberGroups, m => m.ListMemberGroups(ctx, param.target, param.query, flags)),
            Commands.GroupListMembers(var param, var flags) => ctx.Execute<GroupMember>(GroupMemberList, m => m.ListGroupMembers(ctx, param.target, null, flags)),
            Commands.GroupSearchMembers(var param, var flags) => ctx.Execute<GroupMember>(GroupMemberList, m => m.ListGroupMembers(ctx, param.target, param.query, flags)),
            Commands.SystemListGroups(var param, var flags) => ctx.Execute<Groups>(GroupList, g => g.ListSystemGroups(ctx, param.target, null, flags)),
            Commands.SystemSearchGroups(var param, var flags) => ctx.Execute<Groups>(GroupList, g => g.ListSystemGroups(ctx, param.target, param.query, flags)),
            Commands.GroupListGroups(var param, var flags) => ctx.Execute<Groups>(GroupList, g => g.ListSystemGroups(ctx, ctx.System, null, flags)),
            Commands.GroupSearchGroups(var param, var flags) => ctx.Execute<Groups>(GroupList, g => g.ListSystemGroups(ctx, ctx.System, param.query, flags)),
            Commands.GroupNew(var param, _) => ctx.Execute<Groups>(GroupNew, g => g.CreateGroup(ctx, param.name)),
            Commands.GroupInfo(var param, _) => ctx.Execute<Groups>(GroupInfo, g => g.ShowGroupCard(ctx, param.target)),
            Commands.GroupShowName(var param, var flags) => ctx.Execute<Groups>(GroupRename, g => g.ShowGroupDisplayName(ctx, param.target, flags.GetReplyFormat())),
            Commands.GroupClearName(var param, var flags) => ctx.Execute<Groups>(GroupRename, g => g.RenameGroup(ctx, param.target, null)),
            Commands.GroupRename(var param, _) => ctx.Execute<Groups>(GroupRename, g => g.RenameGroup(ctx, param.target, param.name)),
            Commands.GroupShowDisplayName(var param, var flags) => ctx.Execute<Groups>(GroupDisplayName, g => g.ShowGroupDisplayName(ctx, param.target, flags.GetReplyFormat())),
            Commands.GroupClearDisplayName(var param, var flags) => ctx.Execute<Groups>(GroupDisplayName, g => g.ClearGroupDisplayName(ctx, param.target)),
            Commands.GroupChangeDisplayName(var param, _) => ctx.Execute<Groups>(GroupDisplayName, g => g.ChangeGroupDisplayName(ctx, param.target, param.name)),
            Commands.GroupShowDescription(var param, var flags) => ctx.Execute<Groups>(GroupDesc, g => g.ShowGroupDescription(ctx, param.target, flags.GetReplyFormat())),
            Commands.GroupClearDescription(var param, var flags) => ctx.Execute<Groups>(GroupDesc, g => g.ClearGroupDescription(ctx, param.target)),
            Commands.GroupChangeDescription(var param, _) => ctx.Execute<Groups>(GroupDesc, g => g.ChangeGroupDescription(ctx, param.target, param.description)),
            Commands.GroupShowIcon(var param, var flags) => ctx.Execute<Groups>(GroupIcon, g => g.ShowGroupIcon(ctx, param.target, flags.GetReplyFormat())),
            Commands.GroupClearIcon(var param, var flags) => ctx.Execute<Groups>(GroupIcon, g => g.ClearGroupIcon(ctx, param.target)),
            Commands.GroupChangeIcon(var param, _) => ctx.Execute<Groups>(GroupIcon, g => g.ChangeGroupIcon(ctx, param.target, param.icon)),
            Commands.GroupShowBanner(var param, var flags) => ctx.Execute<Groups>(GroupBannerImage, g => g.ShowGroupBanner(ctx, param.target, flags.GetReplyFormat())),
            Commands.GroupClearBanner(var param, var flags) => ctx.Execute<Groups>(GroupBannerImage, g => g.ClearGroupBanner(ctx, param.target)),
            Commands.GroupChangeBanner(var param, _) => ctx.Execute<Groups>(GroupBannerImage, g => g.ChangeGroupBanner(ctx, param.target, param.banner)),
            Commands.GroupShowColor(var param, var flags) => ctx.Execute<Groups>(GroupColor, g => g.ShowGroupColor(ctx, param.target, flags.GetReplyFormat())),
            Commands.GroupClearColor(var param, var flags) => ctx.Execute<Groups>(GroupColor, g => g.ClearGroupColor(ctx, param.target)),
            Commands.GroupChangeColor(var param, _) => ctx.Execute<Groups>(GroupColor, g => g.ChangeGroupColor(ctx, param.target, param.color)),
            Commands.GroupAddMember(var param, var flags) => ctx.Execute<GroupMember>(GroupAdd, g => g.AddRemoveMembers(ctx, param.target, param.targets, Groups.AddRemoveOperation.Add, flags.all)),
            Commands.GroupRemoveMember(var param, var flags) => ctx.Execute<GroupMember>(GroupRemove, g => g.AddRemoveMembers(ctx, param.target, param.targets, Groups.AddRemoveOperation.Remove, flags.all)),
            Commands.GroupShowPrivacy(var param, _) => ctx.Execute<Groups>(GroupPrivacy, g => g.ShowGroupPrivacy(ctx, param.target)),
            Commands.GroupChangePrivacyAll(var param, _) => ctx.Execute<Groups>(GroupPrivacy, g => g.SetAllGroupPrivacy(ctx, param.target, param.level)),
            Commands.GroupChangePrivacy(var param, _) => ctx.Execute<Groups>(GroupPrivacy, g => g.SetGroupPrivacy(ctx, param.target, param.privacy, param.level)),
            Commands.GroupSetPublic(var param, _) => ctx.Execute<Groups>(GroupPrivacy, g => g.SetAllGroupPrivacy(ctx, param.target, PrivacyLevel.Public)),
            Commands.GroupSetPrivate(var param, _) => ctx.Execute<Groups>(GroupPrivacy, g => g.SetAllGroupPrivacy(ctx, param.target, PrivacyLevel.Private)),
            Commands.GroupDelete(var param, var flags) => ctx.Execute<Groups>(GroupDelete, g => g.DeleteGroup(ctx, param.target)),
            Commands.GroupId(var param, _) => ctx.Execute<Groups>(GroupId, g => g.DisplayId(ctx, param.target)),
            Commands.GroupFronterPercent(var param, var flags) => ctx.Execute<SystemFront>(GroupFrontPercent, g => g.FrontPercent(ctx, null, flags.duration, flags.fronters_only, flags.flat, param.target)),
            _ =>
            // this should only ever occur when deving if commands are not implemented...
            ctx.Reply(
                $"{Emojis.Error} Parsed command {ctx.Parameters.Callback().AsCode()} not implemented in PluralKit.Bot!"),
        };
        if (ctx.Match("system", "s", "account", "acc"))
            return HandleSystemCommand(ctx);
        if (ctx.Match("member", "m"))
            return HandleMemberCommand(ctx);
        if (ctx.Match("group", "g"))
            return HandleGroupCommand(ctx);
        if (ctx.Match("switch", "sw"))
            return HandleSwitchCommand(ctx);
        if (ctx.Match("commands", "cmd", "c"))
            return CommandHelpRoot(ctx);
        if (ctx.Match("ap", "autoproxy", "auto"))
            return HandleAutoproxyCommand(ctx);
        if (ctx.Match("config", "cfg", "configure"))
            return HandleConfigCommand(ctx);
        if (ctx.Match("serverconfig", "guildconfig", "scfg"))
            return HandleServerConfigCommand(ctx);
        if (ctx.Match("token"))
            if (ctx.Match("refresh", "renew", "invalidate", "reroll", "regen"))
                return ctx.Execute<Api>(TokenRefresh, m => m.RefreshToken(ctx));
            else
                return ctx.Execute<Api>(TokenGet, m => m.GetToken(ctx));
        if (ctx.Match("import"))
            return ctx.Execute<ImportExport>(Import, m => m.Import(ctx));
        if (ctx.Match("export"))
            return ctx.Execute<ImportExport>(Export, m => m.Export(ctx));
        if (ctx.Match("explain"))
            return ctx.Execute<Help>(Explain, m => m.Explain(ctx));
        if (ctx.Match("message", "msg", "messageinfo"))
            return ctx.Execute<ProxiedMessage>(Message, m => m.GetMessage(ctx));
        if (ctx.Match("edit", "e"))
            return ctx.Execute<ProxiedMessage>(MessageEdit, m => m.EditMessage(ctx, false));
        if (ctx.Match("x"))
            return ctx.Execute<ProxiedMessage>(MessageEdit, m => m.EditMessage(ctx, true));
        if (ctx.Match("reproxy", "rp", "crimes", "crime"))
            return ctx.Execute<ProxiedMessage>(MessageReproxy, m => m.ReproxyMessage(ctx));
        if (ctx.Match("log"))
            if (ctx.Match("channel"))
                return ctx.Execute<ServerConfig>(LogChannel, m => m.SetLogChannel(ctx), true);
            else if (ctx.Match("enable", "on"))
                return ctx.Execute<ServerConfig>(LogEnable, m => m.SetLogEnabled(ctx, true), true);
            else if (ctx.Match("disable", "off"))
                return ctx.Execute<ServerConfig>(LogDisable, m => m.SetLogEnabled(ctx, false), true);
            else if (ctx.Match("list", "show"))
                return ctx.Execute<ServerConfig>(LogShow, m => m.ShowLogDisabledChannels(ctx), true);
            else
                return ctx.Reply($"{Emojis.Warn} Message logging commands have moved to `{ctx.DefaultPrefix}serverconfig`.");
        if (ctx.Match("logclean"))
            return ctx.Execute<ServerConfig>(ServerConfigLogClean, m => m.SetLogCleanup(ctx), true);
        if (ctx.Match("blacklist", "bl"))
            if (ctx.Match("enable", "on", "add", "deny"))
                return ctx.Execute<ServerConfig>(BlacklistAdd, m => m.SetProxyBlacklisted(ctx, true), true);
            else if (ctx.Match("disable", "off", "remove", "allow"))
                return ctx.Execute<ServerConfig>(BlacklistRemove, m => m.SetProxyBlacklisted(ctx, false), true);
            else if (ctx.Match("list", "show"))
                return ctx.Execute<ServerConfig>(BlacklistShow, m => m.ShowProxyBlacklisted(ctx), true);
            else
                return ctx.Reply($"{Emojis.Warn} Blacklist commands have moved to `{ctx.DefaultPrefix}serverconfig`.");
        if (ctx.Match("proxy"))
            if (ctx.Match("debug"))
                return ctx.Execute<Checks>(ProxyCheck, m => m.MessageProxyCheck(ctx));
        if (ctx.Match("invite")) return ctx.Execute<Misc>(Invite, m => m.Invite(ctx));
        if (ctx.Match("mn")) return ctx.Execute<Fun>(null, m => m.Mn(ctx));
        if (ctx.Match("fire")) return ctx.Execute<Fun>(null, m => m.Fire(ctx));
        if (ctx.Match("thunder")) return ctx.Execute<Fun>(null, m => m.Thunder(ctx));
        if (ctx.Match("freeze")) return ctx.Execute<Fun>(null, m => m.Freeze(ctx));
        if (ctx.Match("starstorm")) return ctx.Execute<Fun>(null, m => m.Starstorm(ctx));
        if (ctx.Match("flash")) return ctx.Execute<Fun>(null, m => m.Flash(ctx));
        if (ctx.Match("rool")) return ctx.Execute<Fun>(null, m => m.Rool(ctx));
        if (ctx.Match("sus")) return ctx.Execute<Fun>(null, m => m.Sus(ctx));
        if (ctx.Match("error")) return ctx.Execute<Fun>(null, m => m.Error(ctx));
        if (ctx.Match("stats", "status")) return ctx.Execute<Misc>(null, m => m.Stats(ctx));
        if (ctx.Match("permcheck"))
            return ctx.Execute<Checks>(PermCheck, m => m.PermCheckGuild(ctx));
        if (ctx.Match("proxycheck"))
            return ctx.Execute<Checks>(ProxyCheck, m => m.MessageProxyCheck(ctx));
        if (ctx.Match("debug"))
            return HandleDebugCommand(ctx);
        if (ctx.Match("admin"))
            return HandleAdminCommand(ctx);
        if (ctx.Match("dashboard", "dash"))
            return ctx.Execute<Help>(Dashboard, m => m.Dashboard(ctx));
    }

    private async Task HandleAdminAbuseLogCommand(Context ctx)
    {
        ctx.AssertBotAdmin();

        if (ctx.Match("n", "new", "create"))
            await ctx.Execute<Admin>(Admin, a => a.AbuseLogCreate(ctx));
        else
        {
            AbuseLog? abuseLog = null!;
            var account = await ctx.MatchUser();
            if (account != null)
            {
                abuseLog = await ctx.Repository.GetAbuseLogByAccount(account.Id);
            }
            else
            {
                abuseLog = await ctx.Repository.GetAbuseLogByGuid(new Guid(ctx.PopArgument()));
            }

            if (abuseLog == null)
            {
                await ctx.Reply($"{Emojis.Error} Could not find an existing abuse log entry for that query.");
                return;
            }

            if (!ctx.HasNext())
                await ctx.Execute<Admin>(Admin, a => a.AbuseLogShow(ctx, abuseLog));
            else if (ctx.Match("au", "adduser"))
                await ctx.Execute<Admin>(Admin, a => a.AbuseLogAddUser(ctx, abuseLog));
            else if (ctx.Match("ru", "removeuser"))
                await ctx.Execute<Admin>(Admin, a => a.AbuseLogRemoveUser(ctx, abuseLog));
            else if (ctx.Match("desc", "description"))
                await ctx.Execute<Admin>(Admin, a => a.AbuseLogDescription(ctx, abuseLog));
            else if (ctx.Match("deny", "deny-bot-usage"))
                await ctx.Execute<Admin>(Admin, a => a.AbuseLogFlagDeny(ctx, abuseLog));
            else if (ctx.Match("yeet", "remove", "delete"))
                await ctx.Execute<Admin>(Admin, a => a.AbuseLogDelete(ctx, abuseLog));
            else
                await ctx.Reply($"{Emojis.Error} Unknown subcommand {ctx.PeekArgument().AsCode()}.");
        }
    }

    private async Task HandleAdminCommand(Context ctx)
    {
        if (ctx.Match("usid", "updatesystemid"))
            await ctx.Execute<Admin>(Admin, a => a.UpdateSystemId(ctx));
        else if (ctx.Match("umid", "updatememberid"))
            await ctx.Execute<Admin>(Admin, a => a.UpdateMemberId(ctx));
        else if (ctx.Match("ugid", "updategroupid"))
            await ctx.Execute<Admin>(Admin, a => a.UpdateGroupId(ctx));
        else if (ctx.Match("rsid", "rerollsystemid"))
            await ctx.Execute<Admin>(Admin, a => a.RerollSystemId(ctx));
        else if (ctx.Match("rmid", "rerollmemberid"))
            await ctx.Execute<Admin>(Admin, a => a.RerollMemberId(ctx));
        else if (ctx.Match("rgid", "rerollgroupid"))
            await ctx.Execute<Admin>(Admin, a => a.RerollGroupId(ctx));
        else if (ctx.Match("uml", "updatememberlimit"))
            await ctx.Execute<Admin>(Admin, a => a.SystemMemberLimit(ctx));
        else if (ctx.Match("ugl", "updategrouplimit"))
            await ctx.Execute<Admin>(Admin, a => a.SystemGroupLimit(ctx));
        else if (ctx.Match("sr", "systemrecover"))
            await ctx.Execute<Admin>(Admin, a => a.SystemRecover(ctx));
        else if (ctx.Match("sd", "systemdelete"))
            await ctx.Execute<Admin>(Admin, a => a.SystemDelete(ctx));
        else if (ctx.Match("sendmsg", "sendmessage"))
            await ctx.Execute<Admin>(Admin, a => a.SendAdminMessage(ctx));
        else if (ctx.Match("al", "abuselog"))
            await HandleAdminAbuseLogCommand(ctx);
        else
            await ctx.Reply($"{Emojis.Error} Unknown command.");
    }

    private async Task HandleDebugCommand(Context ctx)
    {
        var availableCommandsStr = "Available debug targets: `permissions`, `proxying`";

        if (ctx.Match("permissions", "perms", "permcheck"))
            if (ctx.Match("channel", "ch"))
                await ctx.Execute<Checks>(PermCheck, m => m.PermCheckChannel(ctx));
            else
                await ctx.Execute<Checks>(PermCheck, m => m.PermCheckGuild(ctx));
        else if (ctx.Match("channel"))
            await ctx.Execute<Checks>(PermCheck, m => m.PermCheckChannel(ctx));
        else if (ctx.Match("proxy", "proxying", "proxycheck"))
            await ctx.Execute<Checks>(ProxyCheck, m => m.MessageProxyCheck(ctx));
        else if (!ctx.HasNext())
            await ctx.Reply($"{Emojis.Error} You need to pass a command. {availableCommandsStr}");
        else
            await ctx.Reply(
                $"{Emojis.Error} Unknown debug command {ctx.PeekArgument().AsCode()}. {availableCommandsStr}");
    }

    private async Task HandleSystemCommand(Context ctx)
    {
        if (ctx.Match("commands", "help"))
            await PrintCommandList(ctx, "systems", SystemCommands);

        // todo: these aren't deprecated but also shouldn't be here
        else if (ctx.Match("webhook", "hook"))
            await ctx.Execute<Api>(null, m => m.SystemWebhook(ctx));

        // finally, parse commands that *can* take a system target
        else
        {
            // TODO: actually implement this
            // // try matching a system ID
            // var target = await ctx.MatchSystem();
            // var previousPtr = ctx.Parameters._ptr;

            // // if we have a parsed target and no more commands, don't bother with the command flow
            // // we skip the `target != null` check here since the argument isn't be popped if it's not a system
            // if (!ctx.HasNext())
            // {
            //     await ctx.Execute<System>(SystemInfo, m => m.Query(ctx, target ?? ctx.System));
            //     return;
            // }

            // // hacky, but we need to CheckSystem(target) which throws a PKError
            // // normally PKErrors are only handled in ctx.Execute
            // try
            // {
            //     await HandleSystemCommandTargeted(ctx, target ?? ctx.System);
            // }
            // catch (PKError e)
            // {
            //     await ctx.Reply($"{Emojis.Error} {e.Message}");
            //     return;
            // }

            // // if we *still* haven't matched anything, the user entered an invalid command name or system reference
            // if (ctx.Parameters._ptr == previousPtr)
            // {
            //     if (!ctx.Parameters.Peek().TryParseHid(out _) && !ctx.Parameters.Peek().TryParseMention(out _))
            //     {
            //         await PrintCommandNotFoundError(ctx, SystemCommands);
            //         return;
            //     }

            //     var list = CreatePotentialCommandList(ctx.DefaultPrefix, SystemCommands);
            //     await ctx.Reply($"{Emojis.Error} {await CreateSystemNotFoundError(ctx)}\n\n"
            //             + $"Perhaps you meant to use one of the following commands?\n{list}");
            // }
        }
    }

    private async Task HandleSystemCommandTargeted(Context ctx, PKSystem target)
    {
        if (ctx.Match("id"))
            await ctx.CheckSystem(target).Execute<System>(SystemId, m => m.DisplayId(ctx, target));
    }

    private async Task HandleMemberCommand(Context ctx)
    {
        if (ctx.Match("commands", "help"))
            await PrintCommandList(ctx, "members", MemberCommands);
        else if (!ctx.HasNext())
            await PrintCommandExpectedError(ctx, MemberNew, MemberInfo, MemberRename, MemberDisplayName,
                MemberServerName, MemberDesc, MemberPronouns,
                MemberColor, MemberBirthday, MemberProxy, MemberDelete, MemberAvatar);
        else
            await ctx.Reply($"{Emojis.Error} {ctx.CreateNotFoundError("Member", ctx.PopArgument())}");
    }

    private async Task HandleGroupCommand(Context ctx)
    {
        // Commands with no group argument
        if (ctx.Match("commands", "help"))
            await PrintCommandList(ctx, "groups", GroupCommands);
        else if (!ctx.HasNext())
            await PrintCommandExpectedError(ctx, GroupCommands);
        else
            await ctx.Reply($"{Emojis.Error} {ctx.CreateNotFoundError("Group", ctx.PopArgument())}");
    }

    private async Task HandleSwitchCommand(Context ctx)
    {
        await PrintCommandNotFoundError(ctx, Switch, SwitchOut, SwitchMove, SwitchEdit, SwitchEditOut,
            SwitchDelete, SwitchCopy, SystemFronter, SystemFrontHistory);
    }

    private async Task CommandHelpRoot(Context ctx)
    {
        if (!ctx.HasNext())
        {
            await ctx.Reply(
                "Available command help targets: `system`, `member`, `group`, `switch`, `config`, `autoproxy`, `log`, `blacklist`."
                + $"\n- **{ctx.DefaultPrefix}commands <target>** - *View commands related to a help target.*"
                + "\n\nFor the full list of commands, see the website: <https://pluralkit.me/commands>");
            return;
        }

        switch (ctx.PeekArgument())
        {
            case "system":
            case "systems":
            case "s":
            case "account":
            case "acc":
                await PrintCommandList(ctx, "systems", SystemCommands);
                break;
            case "member":
            case "members":
            case "m":
                await PrintCommandList(ctx, "members", MemberCommands);
                break;
            case "group":
            case "groups":
            case "g":
                await PrintCommandList(ctx, "groups", GroupCommands);
                break;
            case "switch":
            case "switches":
            case "switching":
            case "sw":
                await PrintCommandList(ctx, "switching", SwitchCommands);
                break;
            case "log":
                await PrintCommandList(ctx, "message logging", LogCommands);
                break;
            case "blacklist":
            case "bl":
                await PrintCommandList(ctx, "channel blacklisting", BlacklistCommands);
                break;
            case "config":
            case "cfg":
                await PrintCommandList(ctx, "settings", ConfigCommands);
                break;
            case "serverconfig":
            case "guildconfig":
            case "scfg":
                await PrintCommandList(ctx, "server settings", ServerConfigCommands);
                break;
            case "autoproxy":
            case "ap":
                await PrintCommandList(ctx, "autoproxy", AutoproxyCommands);
                break;
            default:
                await ctx.Reply("For the full list of commands, see the website: <https://pluralkit.me/commands>");
                break;
        }
    }

    private Task HandleAutoproxyCommand(Context ctx)
    {
        // ctx.CheckSystem();
        // oops, that breaks stuff! PKErrors before ctx.Execute don't actually do anything.
        // so we just emulate checking and throwing an error.
        if (ctx.System == null)
            return ctx.Reply($"{Emojis.Error} {Errors.NoSystemError(ctx.DefaultPrefix).Message}");

        return ctx.Execute<Autoproxy>(AutoproxySet, m => m.SetAutoproxyMode(ctx));
    }

    private Task HandleConfigCommand(Context ctx)
    {
        if (ctx.System == null)
            return ctx.Reply($"{Emojis.Error} {Errors.NoSystemError(ctx.DefaultPrefix).Message}");

        if (!ctx.HasNext())
            return ctx.Execute<Config>(null, m => m.ShowConfig(ctx));

        if (ctx.Match("timezone", "zone", "tz"))
            return ctx.Execute<Config>(null, m => m.SystemTimezone(ctx));
        if (ctx.Match("ping"))
            return ctx.Execute<Config>(null, m => m.SystemPing(ctx));
        if (ctx.MatchMultiple(new[] { "private" }, new[] { "member" }) || ctx.Match("mp"))
            return ctx.Execute<Config>(null, m => m.MemberDefaultPrivacy(ctx));
        if (ctx.MatchMultiple(new[] { "private" }, new[] { "group" }) || ctx.Match("gp"))
            return ctx.Execute<Config>(null, m => m.GroupDefaultPrivacy(ctx));
        if (ctx.MatchMultiple(new[] { "show" }, new[] { "private" }) || ctx.Match("sp"))
            return ctx.Execute<Config>(null, m => m.ShowPrivateInfo(ctx));
        if (ctx.MatchMultiple(new[] { "proxy" }, new[] { "case" }))
            return ctx.Execute<Config>(null, m => m.CaseSensitiveProxyTags(ctx));
        if (ctx.MatchMultiple(new[] { "proxy" }, new[] { "error" }) || ctx.Match("pe"))
            return ctx.Execute<Config>(null, m => m.ProxyErrorMessageEnabled(ctx));
        if (ctx.MatchMultiple(new[] { "split" }, new[] { "id", "ids" }) || ctx.Match("sid", "sids"))
            return ctx.Execute<Config>(null, m => m.HidDisplaySplit(ctx));
        if (ctx.MatchMultiple(new[] { "cap", "caps", "capitalize", "capitalise" }, new[] { "id", "ids" }) || ctx.Match("capid", "capids"))
            return ctx.Execute<Config>(null, m => m.HidDisplayCaps(ctx));
        if (ctx.MatchMultiple(new[] { "pad" }, new[] { "id", "ids" }) || ctx.MatchMultiple(new[] { "id" }, new[] { "pad", "padding" }) || ctx.Match("idpad", "padid", "padids"))
            return ctx.Execute<Config>(null, m => m.HidListPadding(ctx));
        if (ctx.MatchMultiple(new[] { "show" }, new[] { "color", "colour", "colors", "colours" }) || ctx.Match("showcolor", "showcolour", "showcolors", "showcolours", "colorcode", "colorhex"))
            return ctx.Execute<Config>(null, m => m.CardShowColorHex(ctx));
        if (ctx.MatchMultiple(new[] { "name" }, new[] { "format" }) || ctx.Match("nameformat", "nf"))
            return ctx.Execute<Config>(null, m => m.NameFormat(ctx));
        if (ctx.MatchMultiple(new[] { "member", "group" }, new[] { "limit" }) || ctx.Match("limit"))
            return ctx.Execute<Config>(null, m => m.LimitUpdate(ctx));
        if (ctx.MatchMultiple(new[] { "proxy" }, new[] { "switch" }) || ctx.Match("proxyswitch", "ps"))
            return ctx.Execute<Config>(null, m => m.ProxySwitch(ctx));
        if (ctx.MatchMultiple(new[] { "server" }, new[] { "name" }, new[] { "format" }) || ctx.MatchMultiple(new[] { "server", "servername" }, new[] { "format", "nameformat", "nf" }) || ctx.Match("snf", "servernf", "servernameformat", "snameformat"))
            return ctx.Execute<Config>(null, m => m.ServerNameFormat(ctx));

        // todo: maybe add the list of configuration keys here?
        return ctx.Reply($"{Emojis.Error} Could not find a setting with that name. Please see `{ctx.DefaultPrefix}commands config` for the list of possible config settings.");
    }

    private Task HandleServerConfigCommand(Context ctx)
    {
        if (!ctx.HasNext())
            return ctx.Execute<ServerConfig>(null, m => m.ShowConfig(ctx));

        if (ctx.MatchMultiple(new[] { "log" }, new[] { "cleanup", "clean" }) || ctx.Match("logclean"))
            return ctx.Execute<ServerConfig>(null, m => m.SetLogCleanup(ctx));
        if (ctx.MatchMultiple(new[] { "invalid", "unknown" }, new[] { "command" }, new[] { "error", "response" }) || ctx.Match("invalidcommanderror", "unknowncommanderror"))
            return ctx.Execute<ServerConfig>(null, m => m.InvalidCommandResponse(ctx));
        if (ctx.MatchMultiple(new[] { "require", "enforce" }, new[] { "tag", "systemtag" }) || ctx.Match("requiretag", "enforcetag"))
            return ctx.Execute<ServerConfig>(null, m => m.RequireSystemTag(ctx));
        if (ctx.MatchMultiple(new[] { "suppress" }, new[] { "notifications" }) || ctx.Match("proxysilent"))
            return ctx.Execute<ServerConfig>(null, m => m.SuppressNotifications(ctx));
        if (ctx.MatchMultiple(new[] { "log" }, new[] { "channel" }))
            return ctx.Execute<ServerConfig>(null, m => m.SetLogChannel(ctx));
        if (ctx.MatchMultiple(new[] { "log" }, new[] { "blacklist" }))
        {
            if (ctx.Match("enable", "on", "add", "deny"))
                return ctx.Execute<ServerConfig>(null, m => m.SetLogBlacklisted(ctx, true));
            else if (ctx.Match("disable", "off", "remove", "allow"))
                return ctx.Execute<ServerConfig>(null, m => m.SetLogBlacklisted(ctx, false));
            else
                return ctx.Execute<ServerConfig>(null, m => m.ShowLogDisabledChannels(ctx));
        }
        if (ctx.MatchMultiple(new[] { "proxy", "proxying" }, new[] { "blacklist" }))
        {
            if (ctx.Match("enable", "on", "add", "deny"))
                return ctx.Execute<ServerConfig>(null, m => m.SetProxyBlacklisted(ctx, true));
            else if (ctx.Match("disable", "off", "remove", "allow"))
                return ctx.Execute<ServerConfig>(null, m => m.SetProxyBlacklisted(ctx, false));
            else
                return ctx.Execute<ServerConfig>(null, m => m.ShowProxyBlacklisted(ctx));
        }

        // todo: maybe add the list of configuration keys here?
        return ctx.Reply($"{Emojis.Error} Could not find a setting with that name. Please see `{ctx.DefaultPrefix}commands serverconfig` for the list of possible config settings.");
    }
}