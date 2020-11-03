using System;
using System.Threading.Tasks;

using Dapper;

using DSharpPlus.Entities;

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

        public async Task AutoproxyOff(Context ctx)
        {
            if (ctx.MessageContext.AutoproxyMode == AutoproxyMode.Off)
                await ctx.Reply($"{Emojis.Note} Autoproxy is already off in this server.");
            else
            {
                await UpdateAutoproxy(ctx, AutoproxyMode.Off, null);
                await ctx.Reply($"{Emojis.Success} Autoproxy turned off in this server.");
            }
        }

        public async Task AutoproxyLatch(Context ctx)
        {
            if (ctx.MessageContext.AutoproxyMode == AutoproxyMode.Latch)
                await ctx.Reply($"{Emojis.Note} Autoproxy is already set to latch mode in this server. If you want to disable autoproxying, use `pk;autoproxy off`.");
            else
            {
                await UpdateAutoproxy(ctx, AutoproxyMode.Latch, null);
                await ctx.Reply($"{Emojis.Success} Autoproxy set to latch mode in this server. Messages will now be autoproxied using the *last-proxied member* in this server.");
            }
        }

        public async Task AutoproxyLatchTimeout(Context ctx)
        {
            if (!ctx.HasNext())
                if (ctx.System.LatchTimeout == 0) await ctx.Reply("Latch timeout is **disabled** for your system.");
                else await ctx.Reply($"The current latch timeout duration for your system is {ctx.System.LatchTimeout} hour(s).");
            else {
                int newTimeout = 6;
                string resetString = "";
                if (ctx.Match("off", "stop", "cancel", "no", "disable", "remove")) newTimeout = 0;
                else if (ctx.Match("reset", "default"))
                {
                    newTimeout = 6;
                    resetString = "reset (set to 6 hours)";
                }
                else if (!int.TryParse(ctx.RemainderOrNull(), out newTimeout)) throw new PKError("Duration must be an integer.");
                if (resetString == "") resetString = $"set to {newTimeout} hour(s)";
                await _db.Execute(conn => _repo.UpdateSystem(conn, ctx.System.Id, new SystemPatch{LatchTimeout = newTimeout}));
                var resp = (newTimeout != 0) ? $"{Emojis.Success} Latch duration {resetString}." : $"{Emojis.Success} Latch timeout disabled.";
                await ctx.Reply(resp);
            }
        }

        public async Task AutoproxyFront(Context ctx)
        {
            if (ctx.MessageContext.AutoproxyMode == AutoproxyMode.Front)
                await ctx.Reply($"{Emojis.Note} Autoproxy is already set to front mode in this server. If you want to disable autoproxying, use `pk;autoproxy off`.");
            else
            {
                await UpdateAutoproxy(ctx, AutoproxyMode.Front, null);
                await ctx.Reply($"{Emojis.Success} Autoproxy set to front mode in this server. Messages will now be autoproxied using the *current first fronter*, if any.");
            }
        }

        public async Task AutoproxyMember(Context ctx, PKMember member)
        {
            ctx.CheckOwnMember(member);

            await UpdateAutoproxy(ctx, AutoproxyMode.Member, member.Id);
            await ctx.Reply($"{Emojis.Success} Autoproxy set to **{member.NameFor(ctx)}** in this server.");
        }

        public async Task AutoproxyAccount(Context ctx)
        {
            if (ctx.Match("enable", "on"))
                await AutoproxyEnableDisable(ctx, false);
            else if (ctx.Match("disable", "off"))
                await AutoproxyEnableDisable(ctx, true);
            else
            {
                var statusString = ctx.MessageContext.DisableAutoproxy ? "disabled" : "enabled";
                await ctx.Reply(embed: new DiscordEmbedBuilder()
                    .WithDescription($"Autoproxy is currently **{statusString}** for account <@{ctx.Author.Id}>.")
                    .Build());
            }
        }

        private async Task AutoproxyEnableDisable(Context ctx, bool disable)
        {
            var statusString = disable ? "disabled" : "enabled";
            if (ctx.MessageContext.DisableAutoproxy == disable)
            {
                await ctx.Reply($"{Emojis.Note} Autoproxy is already {statusString} for account <@{ctx.Author.Id}>.", mentions: new IMention[]{});
                return;
            }
            var patch = new AccountPatch { DisableAutoproxy = disable };
            await _db.Execute(conn => _repo.UpdateAccount(conn, ctx.Author.Id, patch));
            await ctx.Reply($"{Emojis.Success} Autoproxy {statusString} for account <@{ctx.Author.Id}>.", mentions: new IMention[]{});
        }

        public async Task CreateAutoproxyStatusEmbed(Context ctx)
        {
            var commandList = "**pk;autoproxy latch** - Autoproxies as last-proxied member\n**pk;autoproxy front** - Autoproxies as current (first) fronter\n**pk;autoproxy <member>** - Autoproxies as a specific member";
            var eb = new DiscordEmbedBuilder().WithTitle($"Current autoproxy status (for {ctx.Guild.Name.EscapeMarkdown()})");
            
            var fronters = ctx.MessageContext.LastSwitchMembers;
            var relevantMember = ctx.MessageContext.AutoproxyMode switch
            {
                AutoproxyMode.Front => fronters.Length > 0 ? await _db.Execute(c => _repo.GetMember(c, fronters[0])) : null,
                AutoproxyMode.Member => await _db.Execute(c => _repo.GetMember(c, ctx.MessageContext.AutoproxyMember.Value)),
                _ => null
            };

            switch (ctx.MessageContext.AutoproxyMode) {
                case AutoproxyMode.Off: eb.WithDescription($"Autoproxy is currently **off** in this server. To enable it, use one of the following commands:\n{commandList}");
                    break;
                case AutoproxyMode.Front:
                {
                    if (fronters.Length == 0)
                        eb.WithDescription("Autoproxy is currently set to **front mode** in this server, but there are currently no fronters registered. Use the `pk;switch` command to log a switch.");
                    else
                    {
                        if (relevantMember == null) 
                            throw new ArgumentException("Attempted to print member autoproxy status, but the linked member ID wasn't found in the database. Should be handled appropriately.");
                        eb.WithDescription($"Autoproxy is currently set to **front mode** in this server. The current (first) fronter is **{relevantMember.NameFor(ctx).EscapeMarkdown()}** (`{relevantMember.Hid}`). To disable, type `pk;autoproxy off`.");
                    }

                    break;
                }
                // AutoproxyMember is never null if Mode is Member, this is just to make the compiler shut up
                case AutoproxyMode.Member when relevantMember != null: {
                    eb.WithDescription($"Autoproxy is active for member **{relevantMember.NameFor(ctx)}** (`{relevantMember.Hid}`) in this server. To disable, type `pk;autoproxy off`.");
                    break;
                }
                case AutoproxyMode.Latch:
                    eb.WithDescription("Autoproxy is currently set to **latch mode**, meaning the *last-proxied member* will be autoproxied. To disable, type `pk;autoproxy off`.");
                    break;
                
                default: throw new ArgumentOutOfRangeException();
            }

            if (ctx.MessageContext.DisableAutoproxy) eb.AddField("\u200b", $"{Emojis.Note} Autoproxy is currently **disabled** for your account (<@{ctx.Author.Id}>). To enable it, use `pk;autoproxy account enable`.");

            await ctx.Reply(embed: eb.Build());
        }

        private Task UpdateAutoproxy(Context ctx, AutoproxyMode autoproxyMode, MemberId? autoproxyMember)
        {
            var patch = new SystemGuildPatch {AutoproxyMode = autoproxyMode, AutoproxyMember = autoproxyMember};
            return _db.Execute(conn => _repo.UpsertSystemGuild(conn, ctx.System.Id, ctx.Guild.Id, patch));
        }
    }
}