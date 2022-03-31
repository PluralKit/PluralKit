using System.Text;

using Humanizer;

using PluralKit.Core;

namespace PluralKit.Bot;

public class SystemList
{
    public async Task MemberList(Context ctx, PKSystem target)
    {
        if (target == null) throw Errors.NoSystemError;
        ctx.CheckSystemPrivacy(target.Id, target.MemberListPrivacy);

        // explanation of privacy lookup here:
        // - ParseListOptions checks list access privacy and sets the privacy filter (which members show up in list)
        // - RenderMemberList checks the indivual privacy for each member (NameFor, etc)
        // the own system is always allowed to look up their list
        var opts = ctx.ParseListOptions(ctx.DirectLookupContextFor(target.Id));
        if (ctx.MatchFlag("raw")) {
            var members = (await ctx.Database.Execute(conn => conn.QueryMemberList(target.Id, opts.ToQueryOptions())))
                .ToList();
            
            var showId = ctx.MatchFlag("by-id") || ctx.MatchFlag("bid") || ctx.MatchFlag("id");
            var showDispName = ctx.MatchFlag("by-display-name") || ctx.MatchFlag("bdn");
            var fullText = new StringBuilder();
            foreach (var m in members) {
                var canGetDisplayName = m.DisplayName != null && showDispName;
                var name = m.Name;
                name = canGetDisplayName ? $"`{m.DisplayName} ({name})`" : $"`{name}`";
                name = showId ? $"[`{m.Hid}`] {name}" : $"{name}";
                fullText.Append($"\n{name}");
            }
            await ctx.Reply($"{fullText.ToString()}");
            return;
        }
        await ctx.RenderMemberList(
            ctx.LookupContextFor(target.Id),
            target.Id,
            GetEmbedTitle(target, opts),
            target.Color,
            opts
        );
    }

    private string GetEmbedTitle(PKSystem target, ListOptions opts)
    {
        var title = new StringBuilder("Members of ");

        if (target.Name != null)
            title.Append($"{target.Name} (`{target.Hid}`)");
        else
            title.Append($"`{target.Hid}`");

        if (opts.Search != null)
            title.Append($" matching **{opts.Search.Truncate(100)}**");

        return title.ToString();
    }
}