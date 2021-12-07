using System.Text;

using PluralKit.Core;

namespace PluralKit.Bot;

public class SystemList
{
    public async Task MemberList(Context ctx, PKSystem target)
    {
        if (target == null) throw Errors.NoSystemError;
        ctx.CheckSystemPrivacy(target.Id, target.MemberListPrivacy);

        // explanation of privacy lookup here:
        // - ParseMemberListOptions checks list access privacy and sets the privacy filter (which members show up in list)
        // - RenderMemberList checks the indivual privacy for each member (NameFor, etc)
        // the own system is always allowed to look up their list
        var opts = ctx.ParseMemberListOptions(ctx.DirectLookupContextFor(target.Id));
        await ctx.RenderMemberList(
            ctx.LookupContextFor(target.Id),
            target.Id,
            GetEmbedTitle(target, opts),
            target.Color,
            opts
        );
    }

    private string GetEmbedTitle(PKSystem target, MemberListOptions opts)
    {
        var title = new StringBuilder("Members of ");

        if (target.Name != null)
            title.Append($"{target.Name} (`{target.Hid}`)");
        else
            title.Append($"`{target.Hid}`");

        if (opts.Search != null)
            title.Append($" matching **{opts.Search}**");

        return title.ToString();
    }
}