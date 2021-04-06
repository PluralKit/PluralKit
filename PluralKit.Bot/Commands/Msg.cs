using System.Threading.Tasks;

using App.Metrics;

using PluralKit.Core;

using Myriad.Cache;
using Myriad.Rest;
using Myriad.Gateway;


namespace PluralKit.Bot {
    public class Msg
    {
        private readonly BotConfig _botConfig;
        private readonly IMetrics _metrics;
        private readonly CpuStatService _cpu;
        private readonly ShardInfoService _shards;
        private readonly EmbedService _embeds;
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly IDiscordCache _cache;
        private readonly DiscordApiClient _rest;
        private readonly Cluster _cluster;
        private readonly Bot _bot;

        public Msg(BotConfig botConfig, IMetrics metrics, CpuStatService cpu, ShardInfoService shards, EmbedService embeds, ModelRepository repo, IDatabase db, IDiscordCache cache, DiscordApiClient rest, Bot bot, Cluster cluster)
        {
            _botConfig = botConfig;
            _metrics = metrics;
            _cpu = cpu;
            _shards = shards;
            _embeds = embeds;
            _repo = repo;
            _db = db;
            _cache = cache;
            _rest = rest;
            _bot = bot;
            _cluster = cluster;
        }

        public async Task MessageInfo(Context ctx, FullMessage message)
        {
            await ctx.Reply(embed: await _embeds.CreateMessageInfoEmbed(message));
        }

        public async Task MessageDelete(Context ctx, FullMessage message)
        {
            if (message.System.Id != ctx.System?.Id)
                throw new PKError("You can only delete your own messages.");
            await ctx.Rest.DeleteMessage(message.Message.Channel, message.Message.Mid);
            await ctx.Rest.DeleteMessage(ctx.Message);
        }
    }
}
