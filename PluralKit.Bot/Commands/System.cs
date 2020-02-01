using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Humanizer;
using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;

using PluralKit.Bot.CommandSystem;
using PluralKit.Core;

namespace PluralKit.Bot.Commands
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
            await ctx.Reply($"{Emojis.Success} Your system has been created. Type `pk;system` to view it, and type `pk;help` for more information about commands you can use now.");
        }
    }
}
