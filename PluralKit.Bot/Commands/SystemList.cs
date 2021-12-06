using System.Text;

using PluralKit.Core;

namespace PluralKit.Bot;

public class SystemList
{
    public async Task MemberList(Context ctx, PKSystem target)
    {
        if (target == null) throw Errors.NoSystemError;
        ctx.CheckSystemPrivacy(target, target.MemberListPrivacy);

        var opts = ctx.ParseMemberListOptions(ctx.LookupContextFor(target.Id));
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