using System.Text;

using Humanizer;
using PluralKit.Core;

namespace PluralKit.Bot;

public class SystemList
{
    private readonly EmbedService _embeds;

    public SystemList(EmbedService embeds)
    {
        _embeds = embeds;
    }

    public async Task MemberList(Context ctx, PKSystem target)
    {
        if (target == null) throw Errors.NoSystemError(ctx.DefaultPrefix);
        ctx.CheckSystemPrivacy(target.Id, target.MemberListPrivacy);

        // explanation of privacy lookup here:
        // - ParseListOptions checks list access privacy and sets the privacy filter (which members show up in list)
        // - RenderMemberList checks the indivual privacy for each member (NameFor, etc)
        // the own system is always allowed to look up their list
        var opts = ctx.ParseListOptions(ctx.DirectLookupContextFor(target.Id), ctx.LookupContextFor(target.Id));

        if (ctx.MatchFlag("show-embed", "se"))
        {
            await ctx.RenderMemberList(
                ctx.LookupContextFor(target.Id),
                target.Id,
                await GetEmbedTitle(target, opts, ctx),
                target.Color,
                opts
            );
        }
        else
        {
            await ctx.RenderCV2MemberList(
                ctx.LookupContextFor(target.Id),
                target.Id,
                await GetEmbedTitle(target, opts, ctx),
                target.Color,
                opts,
                _embeds
            );
        }
    }

    private async Task<string> GetEmbedTitle(PKSystem target, ListOptions opts, Context ctx)
    {
        var title = new StringBuilder("Members of ");

        var systemGuildSettings = ctx.Guild != null ? await ctx.Repository.GetSystemGuild(ctx.Guild.Id, target.Id) : null;
        if (systemGuildSettings != null && systemGuildSettings.DisplayName != null)
            title.Append($"{systemGuildSettings.DisplayName}  (`{target.DisplayHid(ctx.Config)}`)");
        else if (target.NameFor(ctx) != null)
            title.Append($"{target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
        else
            title.Append($"`{target.DisplayHid(ctx.Config)}`");

        if (opts.Search != null)
            title.Append($" matching **{opts.Search.Truncate(100)}**");

        return title.ToString();
    }
}