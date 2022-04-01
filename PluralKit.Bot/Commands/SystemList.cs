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
            // Check privacy settings
            ctx.CheckSystemPrivacy(target.Id, target.MemberListPrivacy);

            // Get members
            var members = (await ctx.Database.Execute(conn => conn.QueryMemberList(target.Id, opts.ToQueryOptions())))
                .ToList();
            
            await ctx.Reply(GenerateRawList(members));
            return; // This fixes a bug where the normal (embed-based) user list would still be shown
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

    private string GenerateRawList(Context ctx, PKSystem target, List members) {
        // Get flags
        var showId = ctx.MatchFlag("show-id", "id");
        var showDispName = ctx.MatchFlag("show-display-name", "show-dn", "dn");
        
        // Define variable to store the list
        var fullText = new StringBuilder();

        // Generate the list
        foreach (var m in members) {
            var canGetDisplayName = m.DisplayName != null && showDispName;
            name = canGetDisplayName ? $"`{m.DisplayName} ({m.Name})`" : $"`{m.Name}`";
            name = showId ? $"[`{m.Hid}`] {m.Name}" : $"{m.Name}";
            fullText.Append($"\n{m.Name}");
        }

        // Return stringified list
        return fullText.ToString();
    }
}