using NodaTime;

using Myriad.Builders;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Autoproxy
{
    private readonly IClock _clock;

    public Autoproxy(IClock clock)
    {
        _clock = clock;
    }

    public async Task SetAutoproxyMode(Context ctx)
    {
        // no need to check account here, it's already done at CommandTree
        ctx.CheckGuildContext();

        // for now, just for guild
        // this also creates settings if there are none present
        var settings = await ctx.Repository.GetAutoproxySettings(ctx.System.Id, ctx.Guild.Id, null);

        if (ctx.Match("off", "stop", "cancel", "no", "disable", "remove"))
            await AutoproxyOff(ctx, settings);
        else if (ctx.Match("latch", "last", "proxy", "stick", "sticky", "l"))
            await AutoproxyLatch(ctx, settings);
        else if (ctx.Match("front", "fronter", "switch", "f"))
            await AutoproxyFront(ctx, settings);
        else if (ctx.Match("member"))
            throw new PKSyntaxError("Member-mode autoproxy must target a specific member. Use the `pk;autoproxy <member>` command, where `member` is the name or ID of a member in your system.");
        else if (await ctx.MatchMember() is PKMember member)
            await AutoproxyMember(ctx, member);
        else if (!ctx.HasNext())
            await ctx.Reply(embed: await CreateAutoproxyStatusEmbed(ctx, settings));
        else
            throw new PKSyntaxError($"Invalid autoproxy mode {ctx.PopArgument().AsCode()}.");
    }

    private async Task AutoproxyOff(Context ctx, AutoproxySettings settings)
    {
        if (settings.AutoproxyMode == AutoproxyMode.Off)
        {
            await ctx.Reply($"{Emojis.Note} Autoproxy is already off in this server.");
        }
        else
        {
            await UpdateAutoproxy(ctx, AutoproxyMode.Off, null);
            await ctx.Reply($"{Emojis.Success} Autoproxy turned off in this server.");
        }
    }

    private async Task AutoproxyLatch(Context ctx, AutoproxySettings settings)
    {
        if (settings.AutoproxyMode == AutoproxyMode.Latch)
        {
            await ctx.Reply($"{Emojis.Note} Autoproxy is already set to latch mode in this server. If you want to disable autoproxying, use `pk;autoproxy off`.");
        }
        else
        {
            await UpdateAutoproxy(ctx, AutoproxyMode.Latch, null);
            await ctx.Reply($"{Emojis.Success} Autoproxy set to latch mode in this server. Messages will now be autoproxied using the *last-proxied member* in this server.");
        }
    }

    private async Task AutoproxyFront(Context ctx, AutoproxySettings settings)
    {
        if (settings.AutoproxyMode == AutoproxyMode.Front)
        {
            await ctx.Reply($"{Emojis.Note} Autoproxy is already set to front mode in this server. If you want to disable autoproxying, use `pk;autoproxy off`.");
        }
        else
        {
            await UpdateAutoproxy(ctx, AutoproxyMode.Front, null);
            await ctx.Reply($"{Emojis.Success} Autoproxy set to front mode in this server. Messages will now be autoproxied using the *current first fronter*, if any.");
        }
    }

    private async Task AutoproxyMember(Context ctx, PKMember member)
    {
        ctx.CheckOwnMember(member);

        // todo: why does this not throw an error if the member is already set

        await UpdateAutoproxy(ctx, AutoproxyMode.Member, member.Id);
        await ctx.Reply($"{Emojis.Success} Autoproxy set to **{member.NameFor(ctx)}** in this server.");
    }

    private async Task<Embed> CreateAutoproxyStatusEmbed(Context ctx, AutoproxySettings settings)
    {
        var commandList = "**pk;autoproxy latch** - Autoproxies as last-proxied member"
                        + "\n**pk;autoproxy front** - Autoproxies as current (first) fronter"
                        + "\n**pk;autoproxy <member>** - Autoproxies as a specific member";
        var eb = new EmbedBuilder()
            .Title($"Current autoproxy status (for {ctx.Guild.Name.EscapeMarkdown()})");

        var sw = await ctx.Repository.GetLatestSwitch(ctx.System.Id);
        var fronters = sw == null ? new() : await ctx.Database.Execute(c => ctx.Repository.GetSwitchMembers(c, sw.Id)).ToListAsync();
        var latchTimeout = ctx.Config.LatchTimeout.HasValue ? Duration.FromSeconds(ctx.Config.LatchTimeout.Value) : ProxyMatcher.DefaultLatchExpiryTime;

        var relevantMember = settings.AutoproxyMode switch
        {
            AutoproxyMode.Front => fronters.Count > 0 ? fronters[0] : null,
            AutoproxyMode.Member when settings.AutoproxyMember.HasValue => await ctx.Repository.GetMember(settings.AutoproxyMember.Value),
            AutoproxyMode.Latch when settings.AutoproxyMember.HasValue && ctx.Config.LatchTimeout == 0 => await ctx.Repository.GetMember(settings.AutoproxyMember.Value),
            AutoproxyMode.Latch when settings.AutoproxyMember.HasValue =>
                _clock.GetCurrentInstant() - settings.LastLatchTimestamp > latchTimeout
                    ? null
                    : await ctx.Repository.GetMember(settings.AutoproxyMember.Value),

            _ => null
        };

        switch (settings.AutoproxyMode)
        {
            case AutoproxyMode.Off:
                eb.Description($"Autoproxy is currently **off** in this server. To enable it, use one of the following commands:\n{commandList}");
                break;
            case AutoproxyMode.Front:
                {
                    if (fronters.Count == 0)
                    {
                        eb.Description("Autoproxy is currently set to **front mode** in this server, but there are currently no fronters registered. Use the `pk;switch` command to log a switch.");
                    }
                    else
                    {
                        if (relevantMember == null)
                            throw new ArgumentException("Attempted to print member autoproxy status, but the linked member ID wasn't found in the database. Should be handled appropriately.");
                        eb.Description($"Autoproxy is currently set to **front mode** in this server. The current (first) fronter is **{relevantMember.NameFor(ctx).EscapeMarkdown()}** (`{relevantMember.DisplayHid(ctx.Config)}`). To disable, type `pk;autoproxy off`.");
                    }

                    break;
                }
            case AutoproxyMode.Member:
                {
                    if (relevantMember == null)
                        // just pretend autoproxy is off if the member was deleted
                        // ideally we would set it to off in the database though...
                        eb.Description($"Autoproxy is currently **off** in this server. To enable it, use one of the following commands:\n{commandList}");
                    else
                        eb.Description($"Autoproxy is active for member **{relevantMember.NameFor(ctx)}** (`{relevantMember.DisplayHid(ctx.Config)}`) in this server. To disable, type `pk;autoproxy off`.");

                    break;
                }
            case AutoproxyMode.Latch:
                if (relevantMember == null)
                    eb.Description("Autoproxy is currently set to **latch mode**, meaning the *last-proxied member* will be autoproxied. **No member is currently latched.** To disable, type `pk;autoproxy off`.");
                else
                    eb.Description($"Autoproxy is currently set to **latch mode**, meaning the *last-proxied member* will be autoproxied. The currently latched member is **{relevantMember.NameFor(ctx)}** (`{relevantMember.DisplayHid(ctx.Config)}`). To disable, type `pk;autoproxy off`.");

                break;

            default: throw new ArgumentOutOfRangeException();
        }

        var allowAutoproxy = await ctx.Repository.GetAutoproxyEnabled(ctx.Author.Id);
        if (!allowAutoproxy)
            eb.Field(new Embed.Field("\u200b", $"{Emojis.Note} Autoproxy is currently **disabled** for your account (<@{ctx.Author.Id}>). To enable it, use `pk;autoproxy account enable`."));

        return eb.Build();
    }

    private async Task UpdateAutoproxy(Context ctx, AutoproxyMode autoproxyMode, MemberId? autoproxyMember)
    {
        var patch = new AutoproxyPatch { AutoproxyMode = autoproxyMode, AutoproxyMember = autoproxyMember };
        await ctx.Repository.UpdateAutoproxy(ctx.System.Id, ctx.Guild.Id, null, patch);
    }
}