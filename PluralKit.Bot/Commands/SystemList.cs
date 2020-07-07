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

            var opts = ctx.ParseMemberListOptions(ctx.LookupContextFor(target));
            await ctx.RenderMemberList(ctx.LookupContextFor(target), _db, target.Id, GetEmbedTitle(target, opts), opts);
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
}