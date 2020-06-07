using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dapper;

using NodaTime;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    public class SystemList
    {
        private readonly IClock _clock;
        private readonly DbConnectionFactory _db;
        private readonly ILogger _logger;
        
        public SystemList(DbConnectionFactory db, ILogger logger, IClock clock)
        {
            _db = db;
            _logger = logger;
            _clock = clock;
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
                    eb.WithFooter($"{opts.CreateFilterString()}. {members.Count} results.");
                    renderer.RenderPage(eb, ctx.System, ms);
                    return Task.CompletedTask;
                });
        }

        private async Task<IReadOnlyList<PKListMember>> GetMemberList(PKSystem target, SortFilterOptions opts)
        {
            using var conn = await _db.Obtain();
            var query = opts.BuildQuery();
            var args = new {System = target.Id, opts.Filter};

            var timeBefore = _clock.GetCurrentInstant();
            var results = (await conn.QueryAsync<PKListMember>(query, args)).ToList();
            var timeAfter = _clock.GetCurrentInstant();
            _logger.Debug("Executing sort/filter query `{Query}` with arguments {Args} returning {ResultCount} results in {QueryTime}", query, args, results.Count, timeAfter - timeBefore);

            return results;
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