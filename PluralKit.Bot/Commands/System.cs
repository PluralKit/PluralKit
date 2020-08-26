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

            await ctx.Reply(embed: await _embeds.CreateSystemEmbed(ctx, system, ctx.LookupContextFor(system)));
        }

        public async Task New(Context ctx)
        {
            ctx.CheckNoSystem();

            var systemName = ctx.RemainderOrNull();
            if (systemName != null && systemName.Length > Limits.MaxSystemNameLength)
                throw Errors.SystemNameTooLongError(systemName.Length);
            
            var system = await _data.CreateSystem(systemName);
            await _data.AddAccount(system, ctx.Author.Id);
            await ctx.Reply($"{Emojis.Success} Your system has been created. Type `pk;system` to view it, and type `pk;system help` for more information about commands you can use now. Now that you have that set up, check out the getting started guide on setting up members and proxies: <https://pluralkit.me/start>");
        }
    }
}
