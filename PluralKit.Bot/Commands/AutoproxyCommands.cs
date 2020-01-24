using System;
using System.Linq;
using System.Threading.Tasks;

using Discord;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class AutoproxyCommands
    {
        private IDataStore _data;
        private AutoproxyCacheService _cache;

        public AutoproxyCommands(IDataStore data, AutoproxyCacheService cache)
        {
            _data = data;
            _cache = cache;
        }

        public async Task Autoproxy(Context ctx)
        {
            ctx.CheckSystem().CheckGuildContext();
            
            if (ctx.Match("off", "stop", "cancel", "no"))
                await AutoproxyOff(ctx);
            else if (ctx.Match("latch", "last", "proxy", "stick", "sticky"))
                await AutoproxyLatch(ctx);
            else if (ctx.Match("front", "fronter", "switch"))
                await AutoproxyFront(ctx);
            else if (ctx.Match("member"))
                throw new PKSyntaxError("Member-mode autoproxy must target a specific member. Use the `pk;autoproxy <member>` command, where `member` is the name or ID of a member in your system.");
            else if (await ctx.MatchMember() is PKMember member)
                await AutoproxyMember(ctx, member);
            else if (!ctx.HasNext())
                await ctx.Reply(embed: await CreateAutoproxyStatusEmbed(ctx));
            else
                throw new PKSyntaxError($"Invalid autoproxy mode `{ctx.PopArgument().EscapeMarkdown()}`.");
        }

        private async Task AutoproxyOff(Context ctx)
        {
            var settings = await _data.GetSystemGuildSettings(ctx.System, ctx.Guild.Id);
            if (settings.AutoproxyMode == AutoproxyMode.Off)
            {
                await ctx.Reply($"{Emojis.Note} Autoproxy is already off in this server.");
            }
            else
            {
                settings.AutoproxyMode = AutoproxyMode.Off;
                settings.AutoproxyMember = null;
                await _data.SetSystemGuildSettings(ctx.System, ctx.Guild.Id, settings);
                await _cache.FlushCacheForSystem(ctx.System, ctx.Guild.Id);
                await ctx.Reply($"{Emojis.Success} Autoproxy turned off in this server.");
            }
        }

        private async Task AutoproxyLatch(Context ctx)
        {
            var settings = await _data.GetSystemGuildSettings(ctx.System, ctx.Guild.Id);
            if (settings.AutoproxyMode == AutoproxyMode.Latch)
            {
                await ctx.Reply($"{Emojis.Note} Autoproxy is already set to latch mode in this server. If you want to disable autoproxying, use `pk;autoproxy off`.");
            }
            else
            {
                settings.AutoproxyMode = AutoproxyMode.Latch;
                settings.AutoproxyMember = null;
                await _data.SetSystemGuildSettings(ctx.System, ctx.Guild.Id, settings);
                await _cache.FlushCacheForSystem(ctx.System, ctx.Guild.Id);
                await ctx.Reply($"{Emojis.Success} Autoproxy set to latch mode in this server. Messages will now be autoproxied using the *last-proxied member* in this server.");
            }
        }

        private async Task AutoproxyFront(Context ctx)
        {
            var settings = await _data.GetSystemGuildSettings(ctx.System, ctx.Guild.Id);
            if (settings.AutoproxyMode == AutoproxyMode.Front)
            {
                await ctx.Reply($"{Emojis.Note} Autoproxy is already set to front mode in this server. If you want to disable autoproxying, use `pk;autoproxy off`.");
            }
            else
            {
                settings.AutoproxyMode = AutoproxyMode.Front;
                settings.AutoproxyMember = null;
                await _data.SetSystemGuildSettings(ctx.System, ctx.Guild.Id, settings);
                await _cache.FlushCacheForSystem(ctx.System, ctx.Guild.Id);
                await ctx.Reply($"{Emojis.Success} Autoproxy set to front mode in this server. Messages will now be autoproxied using the *current first fronter*, if any.");
            }
        }

        private async Task AutoproxyMember(Context ctx, PKMember member)
        {
            ctx.CheckOwnMember(member);
            
            var settings = await _data.GetSystemGuildSettings(ctx.System, ctx.Guild.Id);
            settings.AutoproxyMode = AutoproxyMode.Member;
            settings.AutoproxyMember = member.Id;
            await _data.SetSystemGuildSettings(ctx.System, ctx.Guild.Id, settings);
            await _cache.FlushCacheForSystem(ctx.System, ctx.Guild.Id);
            await ctx.Reply($"{Emojis.Success} Autoproxy set to **{member.Name}** in this server.");
        }

        private async Task<Embed> CreateAutoproxyStatusEmbed(Context ctx)
        {
            var settings = await _data.GetSystemGuildSettings(ctx.System, ctx.Guild.Id);

            var commandList = "**pk;autoproxy latch** - Autoproxies as last-proxied member\n**pk;autoproxy front** - Autoproxies as current (first) fronter\n**pk;autoproxy <member>** - Autoproxies as a specific member";
            var eb = new EmbedBuilder().WithTitle($"Current autoproxy status (for {ctx.Guild.Name.EscapeMarkdown()})");

            switch (settings.AutoproxyMode) {
                case AutoproxyMode.Off: eb.WithDescription($"Autoproxy is currently **off** in this server. To enable it, use one of the following commands:\n{commandList}");
                    break;
                case AutoproxyMode.Front: {
                    var lastSwitch = await _data.GetLatestSwitch(ctx.System);
                    if (lastSwitch == null)
                        eb.WithDescription("Autoproxy is currently set to **front mode** in this server, but you have no registered switches. Use the `pk;switch` command to log one.");
                    else
                    {
                        var firstMember = await _data.GetSwitchMembers(lastSwitch).FirstOrDefaultAsync();
                        eb.WithDescription(firstMember == null
                            ? "Autoproxy is currently set to **front mode** in this server, but there are currently no fronters registered."
                            : $"Autoproxy is currently set to **front mode** in this server. The current (first) fronter is **{firstMember.Name.EscapeMarkdown()}** (`{firstMember.Hid}`). To disable, type `pk;autoproxy off`.");
                    }

                    break;
                }
                // AutoproxyMember is never null if Mode is Member, this is just to make the compiler shut up
                case AutoproxyMode.Member when settings.AutoproxyMember != null: {
                    var member = await _data.GetMemberById(settings.AutoproxyMember.Value);
                    eb.WithDescription($"Autoproxy is active for member **{member.Name}** (`{member.Hid}`) in this server. To disable, type `pk;autoproxy off`.");
                    break;
                }
                case AutoproxyMode.Latch:
                    eb.WithDescription($"Autoproxy is currently set to **latch mode**, meaning the *last-proxied member* will be autoproxied. To disable, type `pk;autoproxy off`.");
                    break;
                default: throw new ArgumentOutOfRangeException();
            }

            return eb.Build();
        }
    }
}