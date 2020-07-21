using System.Threading.Tasks;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class System
    {
        private IDataStore _data;
        private EmbedService _embeds;
        
        public System(EmbedService embeds, IDataStore data)
        {
            _embeds = embeds;
            _data = data;
        }
        
        public async Task Query(Context ctx, PKSystem system) {
            if (system == null) throw Errors.NoSystemError;

            var opts = ctx.ParseCardOptions(ctx.LookupContextFor(system));
            await ctx.Reply(embed: await _embeds.CreateSystemEmbed(ctx.Shard, system, ctx.LookupContextFor(system), opts));
        }

        public async Task New(Context ctx)
        {
            ctx.CheckNoSystem();

            var system = await _data.CreateSystem(ctx.RemainderOrNull());
            await _data.AddAccount(system, ctx.Author.Id);
            await ctx.Reply($"{Emojis.Success} Your system has been created. Type `pk;system` to view it, and type `pk;system help` for more information about commands you can use now. Now that you have that set up, check out the getting started guide on setting up members and proxies: <https://pluralkit.me/start>");
        }
    }
}
