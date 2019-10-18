using System.Linq;
using System.Threading.Tasks;

using Discord;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class CommandTree
    {
        public static Command SystemInfo = new Command("system", "system [system]", "uwu");
        public static Command SystemNew = new Command("system new", "system new [name]", "uwu");
        public static Command SystemRename = new Command("system name", "system rename [name]", "uwu");
        public static Command SystemDesc = new Command("system description", "system description [description]", "uwu");
        public static Command SystemTag = new Command("system tag", "system tag [tag]", "uwu");
        public static Command SystemAvatar = new Command("system avatar", "system avatar [url|@mention]", "uwu");
        public static Command SystemDelete = new Command("system delete", "system delete", "uwu");
        public static Command SystemTimezone = new Command("system timezone", "system timezone [timezone]", "uwu");
        public static Command SystemList = new Command("system list", "system list [full]", "uwu");
        public static Command SystemFronter = new Command("system fronter", "system fronter", "uwu");
        public static Command SystemFrontHistory = new Command("system fronthistory", "system fronthistory", "uwu");
        public static Command SystemFrontPercent = new Command("system frontpercent", "system frontpercent [timespan]", "uwu");
        public static Command MemberInfo = new Command("member", "member <member>", "uwu");
        public static Command MemberNew = new Command("member new", "member new <name>", "uwu");
        public static Command MemberRename = new Command("member rename", "member <member> rename <new name>", "uwu");
        public static Command MemberDesc = new Command("member description", "member <member> description [description]", "uwu");
        public static Command MemberPronouns = new Command("member pronouns", "member <member> pronouns [pronouns]", "uwu");
        public static Command MemberColor = new Command("member color", "member <member> color [color]", "uwu");
        public static Command MemberBirthday = new Command("member birthday", "member <member> birthday [birthday]", "uwu");
        public static Command MemberProxy = new Command("member proxy", "member <member> proxy [example proxy]", "uwu");
        public static Command MemberDelete = new Command("member delete", "member <member> delete", "uwu");
        public static Command MemberAvatar = new Command("member avatar", "member <member> avatar [url|@mention]", "uwu");
        public static Command MemberDisplayName = new Command("member displayname", "member <member> displayname [display name]", "uwu");
        public static Command Switch = new Command("switch", "switch <member> [member 2] [member 3...]", "uwu");
        public static Command SwitchOut = new Command("switch out", "switch out", "uwu");
        public static Command SwitchMove = new Command("switch move", "switch move <date/time>", "uwu");
        public static Command SwitchDelete = new Command("switch delete", "switch delete", "uwu");
        public static Command Link = new Command("link", "link <account>", "uwu");
        public static Command Unlink = new Command("unlink", "unlink [account]", "uwu");
        public static Command TokenGet = new Command("token", "token", "uwu");
        public static Command TokenRefresh = new Command("token refresh", "token refresh", "uwu");
        public static Command Import = new Command("import", "import [fileurl]", "uwu");
        public static Command Export = new Command("export", "export", "uwu");
        public static Command HelpCommandList = new Command("commands", "commands", "uwu");
        public static Command HelpProxy = new Command("help proxy", "help proxy", "uwu");
        public static Command Help = new Command("help", "help", "uwu");
        public static Command Message = new Command("message", "message <id|link>", "uwu");
        public static Command Log = new Command("log", "log <channel>", "uwu");
        public static Command Invite = new Command("invite", "invite", "uwu");
        public static Command PermCheck = new Command("permcheck", "permcheck <guild>", "uwu");
        
        private IDiscordClient _client;

        public CommandTree(IDiscordClient client)
        {
            _client = client;
        }

        public Task ExecuteCommand(Context ctx)
        {
            if (ctx.Match("system", "s"))
                return HandleSystemCommand(ctx);
            if (ctx.Match("member", "m"))
                return HandleMemberCommand(ctx);
            if (ctx.Match("switch", "sw"))
                return HandleSwitchCommand(ctx);
            if (ctx.Match("link"))
                return ctx.Execute<LinkCommands>(Link, m => m.LinkSystem(ctx));
            if (ctx.Match("unlink"))
                return ctx.Execute<LinkCommands>(Unlink, m => m.UnlinkAccount(ctx));
            if (ctx.Match("token"))
                if (ctx.Match("refresh", "renew", "invalidate", "reroll", "regen"))
                    return ctx.Execute<APICommands>(TokenRefresh, m => m.RefreshToken(ctx));
                else
                    return ctx.Execute<APICommands>(TokenGet, m => m.GetToken(ctx));
            if (ctx.Match("import"))
                return ctx.Execute<ImportExportCommands>(Import, m => m.Import(ctx));
            if (ctx.Match("export"))
                return ctx.Execute<ImportExportCommands>(Export, m => m.Export(ctx));
            if (ctx.Match("help"))
                if (ctx.Match("commands"))
                    return ctx.Execute<HelpCommands>(HelpCommandList, m => m.CommandList(ctx));
                else if (ctx.Match("proxy"))
                    return ctx.Execute<HelpCommands>(HelpProxy, m => m.HelpProxy(ctx));
                else return ctx.Execute<HelpCommands>(Help, m => m.HelpRoot(ctx));
            if (ctx.Match("commands"))
                return ctx.Execute<HelpCommands>(HelpCommandList, m => m.CommandList(ctx));
            if (ctx.Match("message", "msg"))
                return ctx.Execute<ModCommands>(Message, m => m.GetMessage(ctx));
            if (ctx.Match("log"))
                return ctx.Execute<ModCommands>(Log, m => m.SetLogChannel(ctx));
            if (ctx.Match("invite")) return ctx.Execute<MiscCommands>(Invite, m => m.Invite(ctx));
            if (ctx.Match("mn")) return ctx.Execute<MiscCommands>(null, m => m.Mn(ctx));
            if (ctx.Match("fire")) return ctx.Execute<MiscCommands>(null, m => m.Fire(ctx));
            if (ctx.Match("thunder")) return ctx.Execute<MiscCommands>(null, m => m.Thunder(ctx));
            if (ctx.Match("freeze")) return ctx.Execute<MiscCommands>(null, m => m.Freeze(ctx));
            if (ctx.Match("starstorm")) return ctx.Execute<MiscCommands>(null, m => m.Starstorm(ctx));
            if (ctx.Match("stats")) return ctx.Execute<MiscCommands>(null, m => m.Stats(ctx));
            if (ctx.Match("permcheck"))
                return ctx.Execute<MiscCommands>(PermCheck, m => m.PermCheckGuild(ctx));

            ctx.Reply(
                $"{Emojis.Error} Unknown command `{ctx.PeekArgument().SanitizeMentions()}`. For a list of possible commands, see <https://pluralkit.me/commands>.");
            return Task.CompletedTask;
        }

        private async Task HandleSystemCommand(Context ctx)
        {
            // If we have no parameters, default to self-target
            if (!ctx.HasNext())
                await ctx.Execute<SystemCommands>(SystemInfo, m => m.Query(ctx, ctx.System));

            // First, we match own-system-only commands (ie. no target system parameter)
            else if (ctx.Match("new", "create", "make", "add", "register", "init"))
                await ctx.Execute<SystemCommands>(SystemNew, m => m.New(ctx));
            else if (ctx.Match("name", "rename", "changename"))
                await ctx.Execute<SystemCommands>(SystemRename, m => m.Name(ctx));
            else if (ctx.Match("tag"))
                await ctx.Execute<SystemCommands>(SystemTag, m => m.Tag(ctx));
            else if (ctx.Match("description", "desc", "bio"))
                await ctx.Execute<SystemCommands>(SystemDesc, m => m.Description(ctx));
            else if (ctx.Match("avatar", "picture", "icon", "image", "pic", "pfp"))
                await ctx.Execute<SystemCommands>(SystemAvatar, m => m.SystemAvatar(ctx));
            else if (ctx.Match("delete", "remove", "destroy", "erase", "yeet"))
                await ctx.Execute<SystemCommands>(SystemDelete, m => m.Delete(ctx));
            else if (ctx.Match("timezone", "tz"))
                await ctx.Execute<SystemCommands>(SystemTimezone, m => m.SystemTimezone(ctx));
            else if (ctx.Match("list", "l", "members"))
            {
                if (ctx.Match("f", "full", "big", "details", "long"))
                    await ctx.Execute<SystemCommands>(SystemList, m => m.MemberLongList(ctx, ctx.System));
                else
                    await ctx.Execute<SystemCommands>(SystemList, m => m.MemberShortList(ctx, ctx.System));
            }
            else if (ctx.Match("f", "front", "fronter", "fronters"))
            {
                if (ctx.Match("h", "history"))
                    await ctx.Execute<SystemCommands>(SystemFrontHistory, m => m.SystemFrontHistory(ctx, ctx.System));
                else if (ctx.Match("p", "percent", "%"))
                    await ctx.Execute<SystemCommands>(SystemFrontPercent, m => m.SystemFrontPercent(ctx, ctx.System));
                else
                    await ctx.Execute<SystemCommands>(SystemFronter, m => m.SystemFronter(ctx, ctx.System));
            }
            else if (ctx.Match("fh", "fronthistory", "history", "switches"))
                await ctx.Execute<SystemCommands>(SystemFrontHistory, m => m.SystemFrontHistory(ctx, ctx.System));
            else if (ctx.Match("fp", "frontpercent", "front%", "frontbreakdown"))
                await ctx.Execute<SystemCommands>(SystemFrontPercent, m => m.SystemFrontPercent(ctx, ctx.System));
            else if (!ctx.HasNext()) // Bare command
                await ctx.Execute<SystemCommands>(SystemInfo, m => m.Query(ctx, ctx.System));
            else
                await HandleSystemCommandTargeted(ctx);
        }

        private async Task HandleSystemCommandTargeted(Context ctx)
        {
            // Commands that have a system target (eg. pk;system <system> fronthistory)
            var target = await ctx.MatchSystem();
            if (target == null)
            {
                var list = CreatePotentialCommandList(SystemInfo, SystemNew, SystemRename, SystemTag, SystemDesc, SystemAvatar, SystemDelete, SystemTimezone, SystemList, SystemFronter, SystemFrontHistory, SystemFrontPercent);
                await ctx.Reply(
                    $"{Emojis.Error} {await CreateSystemNotFoundError(ctx)}\n\nPerhaps you meant to use one of the following commands?\n{list}");
            }
            else if (ctx.Match("list", "l", "members"))
            {
                if (ctx.Match("f", "full", "big", "details", "long"))
                    await ctx.Execute<SystemCommands>(SystemList, m => m.MemberLongList(ctx, target));
                else
                    await ctx.Execute<SystemCommands>(SystemList, m => m.MemberShortList(ctx, target));
            }
            else if (ctx.Match("f", "front", "fronter", "fronters"))
            {
                if (ctx.Match("h", "history"))
                    await ctx.Execute<SystemCommands>(SystemFrontHistory, m => m.SystemFrontHistory(ctx, target));
                else if (ctx.Match("p", "percent", "%"))
                    await ctx.Execute<SystemCommands>(SystemFrontPercent, m => m.SystemFrontPercent(ctx, target));
                else
                    await ctx.Execute<SystemCommands>(SystemFronter, m => m.SystemFronter(ctx, target));
            }
            else if (ctx.Match("fh", "fronthistory", "history", "switches"))
                await ctx.Execute<SystemCommands>(SystemFrontHistory, m => m.SystemFrontHistory(ctx, target));
            else if (ctx.Match("fp", "frontpercent", "front%", "frontbreakdown"))
                await ctx.Execute<SystemCommands>(SystemFrontPercent, m => m.SystemFrontPercent(ctx, target));
            else if (ctx.Match("info", "view", "show"))
                await ctx.Execute<SystemCommands>(SystemInfo, m => m.Query(ctx, target));
            else if (!ctx.HasNext())
                await ctx.Execute<SystemCommands>(SystemInfo, m => m.Query(ctx, target));
            else
                await PrintCommandNotFoundError(ctx, SystemList, SystemFronter, SystemFrontHistory, SystemFrontPercent,
                    SystemInfo);
        }
        
        private async Task HandleMemberCommand(Context ctx)
        {
            if (ctx.Match("new", "n", "add", "create", "register"))
                await ctx.Execute<MemberCommands>(MemberNew, m => m.NewMember(ctx));
            else if (await ctx.MatchMember() is PKMember target)
                await HandleMemberCommandTargeted(ctx, target);
            else if (!ctx.HasNext())
                await PrintCommandExpectedError(ctx, MemberNew, MemberInfo, MemberRename, MemberDisplayName, MemberDesc, MemberPronouns,
                    MemberColor, MemberBirthday, MemberProxy, MemberDelete, MemberAvatar);
            else
                await ctx.Reply($"{Emojis.Error} {ctx.CreateMemberNotFoundError(ctx.PopArgument())}");
        }

        private async Task HandleMemberCommandTargeted(Context ctx, PKMember target)
        {
            // Commands that have a member target (eg. pk;member <member> delete)
            if (ctx.Match("rename", "name", "changename", "setname"))
                await ctx.Execute<MemberCommands>(MemberRename, m => m.RenameMember(ctx, target));
            else if (ctx.Match("description", "info", "bio", "text", "desc"))
                await ctx.Execute<MemberCommands>(MemberDesc, m => m.MemberDescription(ctx, target));
            else if (ctx.Match("pronouns", "pronoun"))
                await ctx.Execute<MemberCommands>(MemberPronouns, m => m.MemberPronouns(ctx, target));
            else if (ctx.Match("color", "colour"))
                await ctx.Execute<MemberCommands>(MemberColor, m => m.MemberColor(ctx, target));
            else if (ctx.Match("birthday", "bday", "birthdate", "cakeday", "bdate"))
                await ctx.Execute<MemberCommands>(MemberBirthday, m => m.MemberBirthday(ctx, target));
            else if (ctx.Match("proxy", "tags", "proxytags", "brackets"))
                await ctx.Execute<MemberCommands>(MemberProxy, m => m.MemberProxy(ctx, target));
            else if (ctx.Match("delete", "remove", "destroy", "erase", "yeet"))
                await ctx.Execute<MemberCommands>(MemberDelete, m => m.MemberDelete(ctx, target));
            else if (ctx.Match("avatar", "profile", "picture", "icon", "image", "pfp", "pic"))
                await ctx.Execute<MemberCommands>(MemberAvatar, m => m.MemberAvatar(ctx, target));
            else if (ctx.Match("displayname", "dn", "dname", "nick", "nickname"))
                await ctx.Execute<MemberCommands>(MemberDisplayName, m => m.MemberDisplayName(ctx, target));
            else if (!ctx.HasNext()) // Bare command
                await ctx.Execute<MemberCommands>(MemberInfo, m => m.ViewMember(ctx, target));
            else 
                await PrintCommandNotFoundError(ctx, MemberInfo, MemberRename, MemberDisplayName, MemberDesc, MemberPronouns, MemberColor, MemberBirthday, MemberProxy, MemberDelete, MemberAvatar, SystemList);
        }

        private async Task HandleSwitchCommand(Context ctx)
        {
            if (ctx.Match("out"))
                await ctx.Execute<SwitchCommands>(SwitchOut, m => m.SwitchOut(ctx));
            else if (ctx.Match("move", "shift", "offset"))
                await ctx.Execute<SwitchCommands>(SwitchMove, m => m.SwitchMove(ctx));
            else if (ctx.Match("delete", "remove", "erase", "cancel", "yeet"))
                await ctx.Execute<SwitchCommands>(SwitchDelete, m => m.SwitchDelete(ctx));
            else if (ctx.HasNext()) // there are following arguments
                await ctx.Execute<SwitchCommands>(Switch, m => m.Switch(ctx));
            else
                await PrintCommandNotFoundError(ctx, Switch, SwitchOut, SwitchMove, SwitchDelete, SystemFronter, SystemFrontHistory);
        }

        private async Task PrintCommandNotFoundError(Context ctx, params Command[] potentialCommands)
        {
            var commandListStr = CreatePotentialCommandList(potentialCommands);
            await ctx.Reply(
                $"{Emojis.Error} Unknown command `pk;{ctx.FullCommand}`. Perhaps you meant to use one of the following commands?\n{commandListStr}\n\nFor a full list of possible commands, see <https://pluralkit.me/commands>.");
        }
        
        private async Task PrintCommandExpectedError(Context ctx, params Command[] potentialCommands)
        {
            var commandListStr = CreatePotentialCommandList(potentialCommands);
            await ctx.Reply(
                $"{Emojis.Error} You need to pass a command. Perhaps you meant to use one of the following commands?\n{commandListStr}\n\nFor a full list of possible commands, see <https://pluralkit.me/commands>.");
        }
        
        private static string CreatePotentialCommandList(params Command[] potentialCommands)
        {
            return string.Join("\n", potentialCommands.Select(cmd => $"- `pk;{cmd.Usage}`"));
        }

        private async Task<string> CreateSystemNotFoundError(Context ctx)
        {
            var input = ctx.PopArgument();
            if (input.TryParseMention(out var id))
            {
                // Try to resolve the user ID to find the associated account,
                // so we can print their username.
                var user = await _client.GetUserAsync(id);

                // Print descriptive errors based on whether we found the user or not.
                if (user == null)
                    return $"Account with ID `{id}` not found.";
                return $"Account **{user.Username}#{user.Discriminator}** does not have a system registered.";
            }

            return $"System with ID `{input}` not found.";
        }
    }
}