namespace PluralKit.Bot;

public partial class CommandTree
{
    public static Command SystemInfo = new Command("system", "system [system]", "Looks up information about a system");
    public static Command SystemNew = new Command("system new", "system new [name]", "Creates a new system");
    public static Command SystemRename = new Command("system name", "system [system] rename [name]", "Renames your system");
    public static Command SystemServerName = new Command("system servername", "system [system] servername [name]", "Changes your system displayname for this server");
    public static Command SystemDesc = new Command("system description", "system [system] description [description]", "Changes your system's description");
    public static Command SystemColor = new Command("system color", "system [system] color [color]", "Changes your system's color");
    public static Command SystemTag = new Command("system tag", "system [system] tag [tag]", "Changes your system's tag");
    public static Command SystemPronouns = new Command("system pronouns", "system [system] pronouns [pronouns]", "Changes your system's pronouns");
    public static Command SystemServerTag = new Command("system servertag", "system [system] servertag [tag|enable|disable]", "Changes your system's tag in the current server");
    public static Command SystemAvatar = new Command("system icon", "system [system] icon [url|@mention]", "Changes your system's icon");
    public static Command SystemServerAvatar = new Command("system serveravatar", "system [system] serveravatar [tag]", "Changes your system's icon in the current server");
    public static Command SystemBannerImage = new Command("system banner", "system [system] banner [url]", "Set the system's banner image");
    public static Command SystemDelete = new Command("system delete", "system [system] delete", "Deletes your system");
    public static Command SystemProxy = new Command("system proxy", "system proxy [server id] [on|off]", "Enables or disables message proxying in a specific server");
    public static Command SystemList = new Command("system list", "system [system] list [full]", "Lists a system's members");
    public static Command SystemFind = new Command("system find", "system [system] find [full] <search term>", "Searches a system's members given a search term");
    public static Command SystemFronter = new Command("system fronter", "system [system] fronter", "Shows a system's fronter(s)");
    public static Command SystemFrontHistory = new Command("system fronthistory", "system [system] fronthistory", "Shows a system's front history");
    public static Command SystemFrontPercent = new Command("system frontpercent", "system [system] frontpercent [timespan]", "Shows a system's front breakdown");
    public static Command SystemId = new Command("system id", "system [system] id", "Prints your system's id.");
    public static Command SystemPrivacy = new Command("system privacy", "system [system] privacy <name|avatar|description|members|fronter|fronthistory|all> <public|private>", "Changes your system's privacy settings");
    public static Command ConfigTimezone = new Command("config timezone", "config timezone [timezone]", "Changes your system's time zone");
    public static Command ConfigPing = new Command("config ping", "config ping [on|off]", "Changes your system's ping preferences");
    public static Command ConfigAutoproxyAccount = new Command("config autoproxy account", "config autoproxy account [on|off]", "Toggles autoproxy globally for the current account");
    public static Command ConfigAutoproxyTimeout = new Command("config autoproxy timeout", "config autoproxy timeout [<duration>|off|reset]", "Sets the latch timeout duration for your system");
    public static Command ConfigShowPrivate = new Command("config show private", "config show private [on|off]", "Sets whether private information is shown to linked accounts by default");
    public static Command ConfigMemberDefaultPrivacy = new("config private member", "config private member [on|off]", "Sets whether member privacy is automatically set to private when creating a new member");
    public static Command ConfigGroupDefaultPrivacy = new("config private group", "config private group [on|off]", "Sets whether group privacy is automatically set to private when creating a new group");
    public static Command AutoproxySet = new Command("autoproxy", "autoproxy [off|front|latch|member]", "Sets your system's autoproxy mode for the current server");
    public static Command AutoproxyOff = new Command("autoproxy off", "autoproxy off", "Disables autoproxying for your system in the current server");
    public static Command AutoproxyFront = new Command("autoproxy front", "autoproxy front", "Sets your system's autoproxy in this server to proxy the first member currently registered as front");
    public static Command AutoproxyLatch = new Command("autoproxy latch", "autoproxy latch", "Sets your system's autoproxy in this server to proxy the last manually proxied member");
    public static Command AutoproxyMember = new Command("autoproxy member", "autoproxy <member>", "Sets your system's autoproxy in this server to proxy a specific member");
    public static Command MemberInfo = new Command("member", "member <member>", "Looks up information about a member");
    public static Command MemberNew = new Command("member new", "member new <name>", "Creates a new member");
    public static Command MemberRename = new Command("member rename", "member <member> rename <new name>", "Renames a member");
    public static Command MemberDesc = new Command("member description", "member <member> description [description]", "Changes a member's description");
    public static Command MemberPronouns = new Command("member pronouns", "member <member> pronouns [pronouns]", "Changes a member's pronouns");
    public static Command MemberColor = new Command("member color", "member <member> color [color]", "Changes a member's color");
    public static Command MemberBirthday = new Command("member birthday", "member <member> birthday [birthday]", "Changes a member's birthday");
    public static Command MemberProxy = new Command("member proxy", "member <member> proxy [add|remove] [example proxy]", "Changes, adds, or removes a member's proxy tags");
    public static Command MemberDelete = new Command("member delete", "member <member> delete", "Deletes a member");
    public static Command MemberBannerImage = new Command("member banner", "member <member> banner [url]", "Set the member's banner image");
    public static Command MemberAvatar = new Command("member avatar", "member <member> avatar [url|@mention]", "Changes a member's avatar");
    public static Command MemberGroups = new Command("member group", "member <member> group", "Shows the groups a member is in");
    public static Command MemberGroupAdd = new Command("member group", "member <member> group add <group> [group 2] [group 3...]", "Adds a member to one or more groups");
    public static Command MemberGroupRemove = new Command("member group", "member <member> group remove <group> [group 2] [group 3...]", "Removes a member from one or more groups");
    public static Command MemberServerAvatar = new Command("member serveravatar", "member <member> serveravatar [url|@mention]", "Changes a member's avatar in the current server");
    public static Command MemberDisplayName = new Command("member displayname", "member <member> displayname [display name]", "Changes a member's display name");
    public static Command MemberServerName = new Command("member servername", "member <member> servername [server name]", "Changes a member's display name in the current server");
    public static Command MemberAutoproxy = new Command("member autoproxy", "member <member> autoproxy [on|off]", "Sets whether a member will be autoproxied when autoproxy is set to latch or front mode.");
    public static Command MemberKeepProxy = new Command("member keepproxy", "member <member> keepproxy [on|off]", "Sets whether to include a member's proxy tags when proxying");
    public static Command MemberTts = new Command("member text-to-speech", "member <member> text-to-speech [on|off]", "Sets whether to send a member's messages as text-to-speech messages.");
    public static Command MemberRandom = new Command("system random", "system [system] random", "Shows the info card of a randomly selected member in a system.");
    public static Command MemberId = new Command("member id", "member [member] id", "Prints a member's id.");
    public static Command MemberPrivacy = new Command("member privacy", "member <member> privacy <name|description|birthday|pronouns|metadata|visibility|all> <public|private>", "Changes a members's privacy settings");
    public static Command GroupInfo = new Command("group", "group <name>", "Looks up information about a group");
    public static Command GroupNew = new Command("group new", "group new <name>", "Creates a new group");
    public static Command GroupList = new Command("group list", "group list", "Lists all groups in this system");
    public static Command GroupMemberList = new Command("group members", "group <group> list", "Lists all members in a group");
    public static Command GroupRename = new Command("group rename", "group <group> rename <new name>", "Renames a group");
    public static Command GroupDisplayName = new Command("group displayname", "group <group> displayname [display name]", "Changes a group's display name");
    public static Command GroupDesc = new Command("group description", "group <group> description [description]", "Changes a group's description");
    public static Command GroupColor = new Command("group color", "group <group> color [color]", "Changes a group's color");
    public static Command GroupAdd = new Command("group add", "group <group> add <member> [member 2] [member 3...]", "Adds one or more members to a group");
    public static Command GroupRemove = new Command("group remove", "group <group> remove <member> [member 2] [member 3...]", "Removes one or more members from a group");
    public static Command GroupId = new Command("group id", "group [group] id", "Prints a group's id.");
    public static Command GroupPrivacy = new Command("group privacy", "group <group> privacy <name|description|icon|metadata|visibility|all> <public|private>", "Changes a group's privacy settings");
    public static Command GroupBannerImage = new Command("group banner", "group <group> banner [url]", "Set the group's banner image");
    public static Command GroupIcon = new Command("group icon", "group <group> icon [url|@mention]", "Changes a group's icon");
    public static Command GroupDelete = new Command("group delete", "group <group> delete", "Deletes a group");
    public static Command GroupFrontPercent = new Command("group frontpercent", "group <group> frontpercent [timespan]", "Shows a group's front breakdown.");
    public static Command GroupMemberRandom = new Command("group random", "group <group> random", "Shows the info card of a randomly selected member in a group.");
    public static Command GroupRandom = new Command("system random", "system [system] random group", "Shows the info card of a randomly selected group in a system.");
    public static Command Switch = new Command("switch", "switch <member> [member 2] [member 3...]", "Registers a switch");
    public static Command SwitchOut = new Command("switch out", "switch out", "Registers a switch with no members");
    public static Command SwitchMove = new Command("switch move", "switch move <date/time>", "Moves the latest switch in time");
    public static Command SwitchEdit = new Command("switch edit", "switch edit <member> [member 2] [member 3...]", "Edits the members in the latest switch");
    public static Command SwitchEditOut = new Command("switch edit out", "switch edit out", "Turns the latest switch into a switch-out");
    public static Command SwitchDelete = new Command("switch delete", "switch delete", "Deletes the latest switch");
    public static Command SwitchDeleteAll = new Command("switch delete", "switch delete all", "Deletes all logged switches");
    public static Command Link = new Command("link", "link <account>", "Links your system to another account");
    public static Command Unlink = new Command("unlink", "unlink [account]", "Unlinks your system from an account");
    public static Command TokenGet = new Command("token", "token", "Gets your system's API token");
    public static Command TokenRefresh = new Command("token refresh", "token refresh", "Resets your system's API token");
    public static Command Import = new Command("import", "import [fileurl]", "Imports system information from a data file");
    public static Command Export = new Command("export", "export", "Exports system information to a data file");
    public static Command Help = new Command("help", "help", "Shows help information about PluralKit");
    public static Command Explain = new Command("explain", "explain", "Explains the basics of systems and proxying");
    public static Command Message = new Command("message", "message <id|link> [delete|author]", "Looks up a proxied message");
    public static Command MessageEdit = new Command("edit", "edit [link] <text>", "Edit a previously proxied message");
    public static Command MessageReproxy = new Command("reproxy", "reproxy [link] <member>", "Reproxy a previously proxied message using a different member");
    public static Command ProxyCheck = new Command("debug proxy", "debug proxy [link|reply]", "Checks why your message has not been proxied");
    public static Command LogChannel = new Command("log channel", "log channel <channel>", "Designates a channel to post proxied messages to");
    public static Command LogChannelClear = new Command("log channel", "log channel -clear", "Clears the currently set log channel");
    public static Command LogEnable = new Command("log enable", "log enable all|<channel> [channel 2] [channel 3...]", "Enables message logging in certain channels");
    public static Command LogDisable = new Command("log disable", "log disable all|<channel> [channel 2] [channel 3...]", "Disables message logging in certain channels");
    public static Command LogShow = new Command("log show", "log show", "Displays the current list of channels where logging is disabled");
    public static Command LogClean = new Command("logclean", "logclean [on|off]", "Toggles whether to clean up other bots' log channels");
    public static Command BlacklistShow = new Command("blacklist show", "blacklist show", "Displays the current proxy blacklist");
    public static Command BlacklistAdd = new Command("blacklist add", "blacklist add all|<channel> [channel 2] [channel 3...]", "Adds certain channels to the proxy blacklist");
    public static Command BlacklistRemove = new Command("blacklist remove", "blacklist remove all|<channel> [channel 2] [channel 3...]", "Removes certain channels from the proxy blacklist");
    public static Command Invite = new Command("invite", "invite", "Gets a link to invite PluralKit to other servers");
    public static Command PermCheck = new Command("permcheck", "permcheck <guild>", "Checks whether a server's permission setup is correct");
    public static Command Admin = new Command("admin", "admin", "Super secret admin commands (sshhhh)");

    public static Command[] SystemCommands =
    {
        SystemInfo, SystemNew, SystemRename, SystemServerName, SystemTag, SystemDesc, SystemAvatar, SystemServerAvatar, SystemBannerImage, SystemColor,
        SystemDelete, SystemList, SystemFronter, SystemFrontHistory, SystemFrontPercent, SystemPrivacy, SystemProxy
    };

    public static Command[] MemberCommands =
    {
        MemberInfo, MemberNew, MemberRename, MemberDisplayName, MemberServerName, MemberDesc, MemberPronouns,
        MemberColor, MemberBirthday, MemberProxy, MemberAutoproxy, MemberKeepProxy, MemberTts, MemberGroups, MemberGroupAdd,
        MemberGroupRemove, MemberDelete, MemberAvatar, MemberServerAvatar, MemberBannerImage, MemberPrivacy,
        MemberRandom
    };

    public static Command[] GroupCommands =
    {
        GroupInfo, GroupList, GroupNew, GroupAdd, GroupRemove, GroupMemberList, GroupRename, GroupDesc, GroupIcon,
        GroupBannerImage, GroupColor, GroupPrivacy, GroupDelete
    };

    public static Command[] GroupCommandsTargeted =
    {
        GroupInfo, GroupAdd, GroupRemove, GroupMemberList, GroupRename, GroupDesc, GroupIcon, GroupPrivacy,
        GroupDelete, GroupMemberRandom, GroupFrontPercent
    };

    public static Command[] SwitchCommands =
    {
        Switch, SwitchOut, SwitchMove, SwitchEdit, SwitchEditOut, SwitchDelete, SwitchDeleteAll
    };

    public static Command[] ConfigCommands =
    {
        ConfigAutoproxyAccount, ConfigAutoproxyTimeout, ConfigTimezone, ConfigPing,
        ConfigMemberDefaultPrivacy, ConfigGroupDefaultPrivacy, ConfigShowPrivate
    };

    public static Command[] AutoproxyCommands =
    {
        AutoproxyOff, AutoproxyFront, AutoproxyLatch, AutoproxyMember
    };

    public static Command[] LogCommands = { LogChannel, LogChannelClear, LogEnable, LogDisable, LogShow };

    public static Command[] BlacklistCommands = { BlacklistAdd, BlacklistRemove, BlacklistShow };
}