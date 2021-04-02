using System;
using System.Threading.Tasks;

using Humanizer;

using Myriad.Builders;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Autoproxy
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;

        public Autoproxy(IDatabase db, ModelRepository repo)
        {
            _db = db;
            _repo = repo;
        }

        public async Task SetAutoproxyMode(Context ctx)
        {
            // no need to check account here, it's already done at CommandTree
            ctx.CheckGuildContext();

            var scope = ctx.MatchAutoproxyScope();
            ulong? location = ScopeLocation(ctx, scope);

            await using var conn = await _db.Obtain();
            var settings = await _repo.GetAutoproxySettings(conn, ctx.System.Id, scope, location);
            
            if (await ctx.MatchClear())
                await AutoproxyClear(ctx, scope, location, settings);
            else if (ctx.Match("off", "stop", "cancel", "no", "disable", "remove"))
                await AutoproxyOff(ctx, scope, location, settings);
            else if (ctx.Match("latch", "last", "proxy", "stick", "sticky"))
                await AutoproxyLatch(ctx, scope, location, settings);
            else if (ctx.Match("front", "fronter", "switch"))
                await AutoproxyFront(ctx, scope, location, settings);
            else if (ctx.Match("member"))
                throw new PKSyntaxError("Member-mode autoproxy must target a specific member. Use the `pk;autoproxy <member>` command, where `member` is the name or ID of a member in your system.");
            else if (await ctx.MatchMember() is PKMember member)
                await AutoproxyMember(ctx, member, scope, location, settings);
            else if (!ctx.HasNext())
                await ctx.Reply(embed: await AutoproxyStatus(ctx, scope, settings));
            else
                throw new PKSyntaxError($"Invalid autoproxy mode {ctx.PopArgument().AsCode()}.");
        }

        private async Task AutoproxyClear(Context ctx, AutoproxyScope scope, ulong? location, AutoproxySettings settings)
        {
            var scopeStr = ScopeStr(scope);

            if (settings == null)
                await ctx.Reply($"{Emojis.Note} There are no autoproxy settings to clear {scopeStr}.");
            else
            {
                await _db.Execute(conn => _repo.ClearAutoproxySettings(conn, ctx.System.Id, scope, location));
                await ctx.Reply($"{Emojis.Success} Autoproxy settings cleared {scopeStr}.");
            }
        }

        private async Task AutoproxyOff(Context ctx, AutoproxyScope scope, ulong? location, AutoproxySettings settings)
        {
            var scopeStr = ScopeStr(scope);

            // TODO
            if (ctx.MessageContext.AutoproxyMode == AutoproxyMode.Off)
                await ctx.Reply($"{Emojis.Note} Autoproxy is already off {scopeStr}.");
            else
            {
                await UpdateAutoproxy(ctx, AutoproxyMode.Off, scope, location, null);
                await ctx.Reply($"{Emojis.Success} Autoproxy turned off {scopeStr}.");
            }
        }

        private async Task AutoproxyLatch(Context ctx, AutoproxyScope scope, ulong? location, AutoproxySettings settings)
        {
            var scopeStr = ScopeStr(scope);

            if (settings?.Mode == AutoproxyMode.Latch)
                await ctx.Reply($"{Emojis.Note} Autoproxy is already set to latch mode {scopeStr}. If you want to disable autoproxying, use `pk;autoproxy off`.");
            else
            {
                await UpdateAutoproxy(ctx, AutoproxyMode.Latch, scope, location, null);
                await ctx.Reply($"{Emojis.Success} Autoproxy set to latch mode {scopeStr}. Messages will now be autoproxied using the *last-proxied member* {scopeStr}.");
            }
        }

        private async Task AutoproxyFront(Context ctx, AutoproxyScope scope, ulong? location, AutoproxySettings settings)
        {
            var scopeStr = ScopeStr(scope);
         
            if (settings?.Mode == AutoproxyMode.Front)
                await ctx.Reply($"{Emojis.Note} Autoproxy is already set to front mode {scopeStr}. If you want to disable autoproxying, use `pk;autoproxy off`.");
            else
            {
                await UpdateAutoproxy(ctx, AutoproxyMode.Front, scope, location, null);
                await ctx.Reply($"{Emojis.Success} Autoproxy set to front mode {scopeStr}. Messages will now be autoproxied using the *current first fronter*, if any.");
            }
        }

        private async Task AutoproxyMember(Context ctx, PKMember member, AutoproxyScope scope, ulong? location, AutoproxySettings settings)
        {
            ctx.CheckOwnMember(member);

            var scopeStr = ScopeStr(scope);

            if (settings?.Mode == AutoproxyMode.Member && settings?.Member == member.Id)
                await ctx.Reply($"{Emojis.Note} Autoproxy is already set to **{member.NameFor(ctx)}** {scopeStr}. If you want to disable autoproxying, use `pk;autoproxy off`.");
            else {
                await UpdateAutoproxy(ctx, AutoproxyMode.Member, scope, location, member.Id);
                await ctx.Reply($"{Emojis.Success} Autoproxy set to **{member.NameFor(ctx)}** {scopeStr}.");
            }
        }

        private async Task<Embed> AutoproxyStatus(Context ctx, AutoproxyScope scope, AutoproxySettings settings)
        {
            var titleScopeName = "";

            if (scope == AutoproxyScope.Channel)
                titleScopeName = $"(for #{ctx.Channel.Name})";
            else if (scope == AutoproxyScope.Guild)
                titleScopeName = $"(for {ctx.Guild.Name.EscapeMarkdown()})";
            else if (scope == AutoproxyScope.Global)
                titleScopeName = "(global)";

            if (settings == null)
                return await CreateAutoproxyStatusEmbed(ctx, AutoproxyMode.Off, null, ScopeStr(scope), titleScopeName);
            return await CreateAutoproxyStatusEmbed(ctx, settings.Mode, settings.Member, ScopeStr(scope), titleScopeName);
        }

        private async Task<Embed> CreateAutoproxyStatusEmbed(Context ctx, AutoproxyMode mode, MemberId? autoproxyMember, string scopeStr, string titleScopeName)
        {
            var commandList = "**pk;autoproxy latch** - Autoproxies as last-proxied member\n**pk;autoproxy front** - Autoproxies as current (first) fronter\n**pk;autoproxy <member>** - Autoproxies as a specific member";
            var eb = new EmbedBuilder()
                .Title($"Current autoproxy status {titleScopeName}");
            
            var fronters = ctx.MessageContext.LastSwitchMembers;
            var relevantMember = mode switch
            {
                AutoproxyMode.Front => fronters.Length > 0 ? await _db.Execute(c => _repo.GetMember(c, fronters[0])) : null,
                AutoproxyMode.Member => autoproxyMember.HasValue ? await _db.Execute(c => _repo.GetMember(c, autoproxyMember.Value)) : null,
                _ => null
            };

            switch (ctx.MessageContext.AutoproxyMode) {
                case AutoproxyMode.Off:
                    eb.Description($"Autoproxy is currently **off** {scopeStr}. To enable it, use one of the following commands:\n{commandList}");
                    break;
                case AutoproxyMode.Front:
                {
                    if (fronters.Length == 0)
                        eb.Description("Autoproxy is currently set to **front mode** {scopeStr}, but there are currently no fronters registered. Use the `pk;switch` command to log a switch.");
                    else
                    {
                        if (relevantMember == null) 
                            throw new ArgumentException("Attempted to print member autoproxy status, but the linked member ID wasn't found in the database. Should be handled appropriately.");
                        eb.Description($"Autoproxy is currently set to **front mode** {scopeStr}. The current (first) fronter is **{relevantMember.NameFor(ctx).EscapeMarkdown()}** (`{relevantMember.Hid}`). To disable, type `pk;autoproxy off`.");
                    }

                    break;
                }
                // AutoproxyMember is never null if Mode is Member, this is just to make the compiler shut up
                case AutoproxyMode.Member when relevantMember != null: {
                    eb.Description($"Autoproxy is active for member **{relevantMember.NameFor(ctx)}** (`{relevantMember.Hid}`) {scopeStr}. To disable, type `pk;autoproxy off`.");
                    break;
                }
                case AutoproxyMode.Latch:
                    eb.Description("Autoproxy is currently set to **latch mode**, meaning the *last-proxied member* will be autoproxied. To disable, type `pk;autoproxy off`.");
                    break;
                
                default: throw new ArgumentOutOfRangeException();
            }

            if (!ctx.MessageContext.AllowAutoproxy) 
                eb.Field(new("\u200b", $"{Emojis.Note} Autoproxy is currently **disabled** for your account (<@{ctx.Author.Id}>). To enable it, use `pk;autoproxy account enable`."));

            return eb.Build();
        }

        public async Task AutoproxyTimeout(Context ctx)
        {
            if (!ctx.HasNext())
            {
                var timeout = ctx.System.LatchTimeout.HasValue
                    ? Duration.FromSeconds(ctx.System.LatchTimeout.Value) 
                    : (Duration?) null;
                
                if (timeout == null)
                    await ctx.Reply($"You do not have a custom autoproxy timeout duration set. The default latch timeout duration is {ProxyMatcher.DefaultLatchExpiryTime.ToTimeSpan().Humanize()}.");
                else if (timeout == Duration.Zero)
                    await ctx.Reply("Latch timeout is currently **disabled** for your system. Latch mode autoproxy will never time out.");
                else
                    await ctx.Reply($"The current latch timeout duration for your system is {timeout.Value.ToTimeSpan().Humanize()}.");
                return;
            }

            // todo: somehow parse a more human-friendly date format
            int newTimeoutHours;
            if (ctx.Match("off", "stop", "cancel", "no", "disable", "remove")) newTimeoutHours = 0;
            else if (ctx.Match("reset", "default")) newTimeoutHours = -1;
            else if (!int.TryParse(ctx.RemainderOrNull(), out newTimeoutHours)) throw new PKError("Duration must be a number of hours.");

            int? overflow = null;
            if (newTimeoutHours > 100000)
            {
                // sanity check to prevent seconds overflow if someone types in 999999999
                overflow = newTimeoutHours;
                newTimeoutHours = 0;
            }

            var newTimeout = newTimeoutHours > -1 ? Duration.FromHours(newTimeoutHours) : (Duration?) null;
            await _db.Execute(conn => _repo.UpdateSystem(conn, ctx.System.Id, 
                new SystemPatch { LatchTimeout = (int?) newTimeout?.TotalSeconds }));
            
            if (newTimeoutHours == -1)
                await ctx.Reply($"{Emojis.Success} Latch timeout reset to default ({ProxyMatcher.DefaultLatchExpiryTime.ToTimeSpan().Humanize()}).");
            else if (newTimeoutHours == 0 && overflow != null)
                await ctx.Reply($"{Emojis.Success} Latch timeout disabled. Latch mode autoproxy will never time out. ({overflow} hours is too long)");
            else if (newTimeoutHours == 0)
                await ctx.Reply($"{Emojis.Success} Latch timeout disabled. Latch mode autoproxy will never time out.");
            else
                await ctx.Reply($"{Emojis.Success} Latch timeout set to {newTimeout.Value!.ToTimeSpan().Humanize()}.");
        }

        public async Task AutoproxyAccount(Context ctx)
        {
            // todo: this might be useful elsewhere, consider moving it to ctx.MatchToggle
            if (ctx.Match("enable", "on"))
                await AutoproxyEnableDisable(ctx, true);
            else if (ctx.Match("disable", "off"))
                await AutoproxyEnableDisable(ctx, false);
            else if (ctx.HasNext())
                throw new PKSyntaxError("You must pass either \"on\" or \"off\".");
            else
            {
                var statusString = ctx.MessageContext.AllowAutoproxy ? "enabled" : "disabled";
                await ctx.Reply($"Autoproxy is currently **{statusString}** for account <@{ctx.Author.Id}>.");
            }
        }

        private async Task AutoproxyEnableDisable(Context ctx, bool allow)
        {
            var statusString = allow ? "enabled" : "disabled";
            if (ctx.MessageContext.AllowAutoproxy == allow)
            {
                await ctx.Reply($"{Emojis.Note} Autoproxy is already {statusString} for account <@{ctx.Author.Id}>.");
                return;
            }
            var patch = new AccountPatch { AllowAutoproxy = allow };
            await _db.Execute(conn => _repo.UpdateAccount(conn, ctx.Author.Id, patch));
            await ctx.Reply($"{Emojis.Success} Autoproxy {statusString} for account <@{ctx.Author.Id}>.");
        }

        private string ScopeStr(AutoproxyScope scope)
        {
            switch(scope)
            {
                case AutoproxyScope.Channel:
                    return "in this channel";
                case AutoproxyScope.Guild:
                    return "in this server";
                case AutoproxyScope.Global:
                    return "globally";
                default:
                    return "invalid-scope";
            }
        }

        private ulong? ScopeLocation(Context ctx, AutoproxyScope scope)
        {
            // TODO: is this useful elsewhere?
            switch(scope)
            {
                case AutoproxyScope.Channel:
                    return ctx.Channel.Id;
                case AutoproxyScope.Guild:
                    return ctx.Guild.Id;
                case AutoproxyScope.Global:
                    return null;
                default:
                    return null;
            }
        }

        private Task UpdateAutoproxy(Context ctx, AutoproxyMode mode, AutoproxyScope scope, ulong? location, MemberId? autoproxyMember)
        {
            var patch = new AutoproxyPatch { AutoproxyMode = mode, AutoproxyMember = autoproxyMember };
            return _db.Execute(conn => _repo.UpsertAutoproxySettings(conn, ctx.System.Id, location, scope, patch));
        }
    }
}