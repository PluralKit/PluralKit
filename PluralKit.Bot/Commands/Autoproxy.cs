using Humanizer;

using Myriad.Builders;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot;

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

        if (ctx.Match("off", "stop", "cancel", "no", "disable", "remove"))
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
            throw new PKSyntaxError($"Invalid autoproxy mode {ctx.PopArgument().AsCode()}.");
    }

    private async Task AutoproxyOff(Context ctx)
    {
        if (ctx.MessageContext.AutoproxyMode == AutoproxyMode.Off)
        {
            await ctx.Reply($"{Emojis.Note} Autoproxy is already off in this server.");
        }
        else
        {
            await UpdateAutoproxy(ctx, AutoproxyMode.Off, null);
            await ctx.Reply($"{Emojis.Success} Autoproxy turned off in this server.");
        }
    }

    private async Task AutoproxyLatch(Context ctx)
    {
        if (ctx.MessageContext.AutoproxyMode == AutoproxyMode.Latch)
        {
            await ctx.Reply($"{Emojis.Note} Autoproxy is already set to latch mode in this server. If you want to disable autoproxying, use `pk;autoproxy off`.");
        }
        else
        {
            await UpdateAutoproxy(ctx, AutoproxyMode.Latch, null);
            await ctx.Reply($"{Emojis.Success} Autoproxy set to latch mode in this server. Messages will now be autoproxied using the *last-proxied member* in this server.");
        }
    }

    private async Task AutoproxyFront(Context ctx)
    {
        if (ctx.MessageContext.AutoproxyMode == AutoproxyMode.Front)
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

        await UpdateAutoproxy(ctx, AutoproxyMode.Member, member.Id);
        await ctx.Reply($"{Emojis.Success} Autoproxy set to **{member.NameFor(ctx)}** in this server.");
    }

    private async Task<Embed> CreateAutoproxyStatusEmbed(Context ctx)
    {
        var commandList = "**pk;autoproxy latch** - Autoproxies as last-proxied member"
                        + "\n**pk;autoproxy front** - Autoproxies as current (first) fronter"
                        + "\n**pk;autoproxy <member>** - Autoproxies as a specific member";
        var eb = new EmbedBuilder()
            .Title($"Current autoproxy status (for {ctx.Guild.Name.EscapeMarkdown()})");

        var fronters = ctx.MessageContext.LastSwitchMembers;
        var relevantMember = ctx.MessageContext.AutoproxyMode switch
        {
            AutoproxyMode.Front => fronters.Length > 0 ? await _repo.GetMember(fronters[0]) : null,
            AutoproxyMode.Member => await _repo.GetMember(ctx.MessageContext.AutoproxyMember.Value),
            _ => null
        };

        switch (ctx.MessageContext.AutoproxyMode)
        {
            case AutoproxyMode.Off:
                eb.Description($"Autoproxy is currently **off** in this server. To enable it, use one of the following commands:\n{commandList}");
                break;
            case AutoproxyMode.Front:
                {
                    if (fronters.Length == 0)
                    {
                        eb.Description("Autoproxy is currently set to **front mode** in this server, but there are currently no fronters registered. Use the `pk;switch` command to log a switch.");
                    }
                    else
                    {
                        if (relevantMember == null)
                            throw new ArgumentException("Attempted to print member autoproxy status, but the linked member ID wasn't found in the database. Should be handled appropriately.");
                        eb.Description($"Autoproxy is currently set to **front mode** in this server. The current (first) fronter is **{relevantMember.NameFor(ctx).EscapeMarkdown()}** (`{relevantMember.Hid}`). To disable, type `pk;autoproxy off`.");
                    }

                    break;
                }
            // AutoproxyMember is never null if Mode is Member, this is just to make the compiler shut up
            case AutoproxyMode.Member when relevantMember != null:
                {
                    eb.Description($"Autoproxy is active for member **{relevantMember.NameFor(ctx)}** (`{relevantMember.Hid}`) in this server. To disable, type `pk;autoproxy off`.");
                    break;
                }
            case AutoproxyMode.Latch:
                eb.Description("Autoproxy is currently set to **latch mode**, meaning the *last-proxied member* will be autoproxied. To disable, type `pk;autoproxy off`.");
                break;

            default: throw new ArgumentOutOfRangeException();
        }

        if (!ctx.MessageContext.AllowAutoproxy)
            eb.Field(new Embed.Field("\u200b", $"{Emojis.Note} Autoproxy is currently **disabled** for your account (<@{ctx.Author.Id}>). To enable it, use `pk;autoproxy account enable`."));

        return eb.Build();
    }

    private async Task UpdateAutoproxy(Context ctx, AutoproxyMode autoproxyMode, MemberId? autoproxyMember)
    {
        await _repo.GetSystemGuild(ctx.Guild.Id, ctx.System.Id);

        var patch = new SystemGuildPatch { AutoproxyMode = autoproxyMode, AutoproxyMember = autoproxyMember };
        await _repo.UpdateSystemGuild(ctx.System.Id, ctx.Guild.Id, patch);
    }
}