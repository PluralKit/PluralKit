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

            await ctx.Reply(embed: await _embeds.CreateSystemEmbed(system, ctx.LookupContextFor(system)));
        }
        
        public async Task New(Context ctx)
        {
            ctx.CheckNoSystem();

            var system = await _data.CreateSystem(ctx.RemainderOrNull());
            await _data.AddAccount(system, ctx.Author.Id);
            await ctx.Reply($"{Emojis.Success} Your system has been created. Type `pk;system` to view it, and type `pk;system help` for more information about commands you can use now. Now that you have that set up, check out the getting started guide on setting up members and proxies: <https://pluralkit.me/start>");
        }

        public async Task Refresh(Context ctx)
        {
            ctx.CheckSystem();
            await _data.InvalidateSystemCache(ctx.System);
            await ctx.Reply("Cleared the cache for your system. Changes made through the API should now be reflected in the bot.");
        } 
    }
}
