using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class SystemList
    {
        private readonly IDatabase _db;
        
        public SystemList(IDatabase db)
        {
            _db = db;
        }

        public async Task MemberList(Context ctx, PKSystem target)
        {
            if (target == null) throw Errors.NoSystemError;
            ctx.CheckSystemPrivacy(target, target.MemberListPrivacy);

            // GetRendererFor must be called before GetOptions as it consumes a potential positional full argument that'd otherwise land in the filter
            var renderer = GetRendererFor(ctx);
            var opts = GetOptions(ctx, target);
            
            var members = (await _db.Execute(c => opts.Execute(c, target, ctx.LookupContextFor(target)))).ToList();
            await ctx.Paginate(
                members.ToAsyncEnumerable(),
                members.Count,
                renderer.MembersPerPage,
                GetEmbedTitle(target, opts),
                (eb, ms) =>
                {
                    eb.WithFooter($"{opts.CreateFilterString()}. {members.Count} results.");
                    renderer.RenderPage(eb, ctx.System?.Zone ?? DateTimeZone.Utc, ms, ctx.LookupContextFor(target));
                    return Task.CompletedTask;
                });
        }

        private string GetEmbedTitle(PKSystem target, SortFilterOptions opts)
        {
            var title = new StringBuilder("Members of ");
            
            if (target.Name != null) title.Append($"{target.Name.SanitizeMentions()} (`{target.Hid}`)");
            else title.Append($"`{target.Hid}`");
 
            if (opts.Filter != null) title.Append($" matching **{opts.Filter.SanitizeMentions()}**");
            
            return title.ToString();
        }

        private SortFilterOptions GetOptions(Context ctx, PKSystem target)
        {
            var opts = SortFilterOptions.FromFlags(ctx);
            opts.Filter = ctx.RemainderOrNull();
            // If we're *explicitly* trying to access non-public members of another system, error
            if (opts.PrivacyFilter != PrivacyFilter.PublicOnly && ctx.LookupContextFor(target) != LookupContext.ByOwner)
                throw new PKError("You cannot look up private members of another system.");
            return opts;
        }

        private IListRenderer GetRendererFor(Context ctx)
        {
            var longList = ctx.Match("f", "full", "big", "details", "long") || ctx.MatchFlag("f", "full");
            if (longList)
                return new LongRenderer(LongRenderer.MemberFields.FromFlags(ctx));
            return new ShortRenderer();
        }
    }
}