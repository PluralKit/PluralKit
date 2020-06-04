using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dapper;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class SystemList
    {
        private readonly DbConnectionFactory _db;
        
        public SystemList(DbConnectionFactory db)
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
            var members = await GetMemberList(target, opts);
            await ctx.Paginate(
                members.ToAsyncEnumerable(),
                members.Count,
                renderer.MembersPerPage,
                GetEmbedTitle(target, opts),
                (eb, ms) =>
                {
                    eb.WithFooter($"{members.Count} total.");
                    renderer.RenderPage(eb, ms);
                    return Task.CompletedTask;
                });
        }

        private async Task<IReadOnlyList<PKListMember>> GetMemberList(PKSystem target, SortFilterOptions opts)
        {
            using var conn = await _db.Obtain();
            var args = new {System = target.Id, opts.Filter};
            return (await conn.QueryAsync<PKListMember>(opts.BuildQuery(), args)).ToList();
        }

        private string GetEmbedTitle(PKSystem target, SortFilterOptions opts)
        {
            var title = new StringBuilder("Members of ");
            
            if (target.Name != null) title.Append($"{target.Name.SanitizeMentions()} (`{target.Hid}`)");
            else title.Append($"`{target.Hid}`");
 
            if (opts.Filter != null) title.Append($"matching **{opts.Filter.SanitizeMentions()}**");
            
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