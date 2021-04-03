using System.Threading.Tasks;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class System
    {
        private readonly EmbedService _embeds;
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        
        public System(EmbedService embeds, IDatabase db, ModelRepository repo)
        {
            _embeds = embeds;
            _db = db;
            _repo = repo;
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

            var system = _db.Execute(async c =>
            {
                var system = await _repo.CreateSystem(c, systemName);
                await _repo.AddAccount(c, system.Id, ctx.Author.Id);
                return system;
            });
            
            // TODO: better message, perhaps embed like in groups?
            await ctx.Reply($"{Emojis.Success} Your system has been created. Type `pk;system` to view it, and type `pk;system help` for more information about commands you can use now. Now that you have that set up, check out the getting started guide on setting up members and proxies: <https://pluralkit.me/start>");
        }
    }
}
