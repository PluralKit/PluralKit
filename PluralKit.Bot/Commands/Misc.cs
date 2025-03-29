using System.Diagnostics;

using App.Metrics;

using Myriad.Builders;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using Newtonsoft.Json;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Misc
{
    private readonly BotConfig _botConfig;
    private readonly CpuStatService _cpu;
    private readonly IMetrics _metrics;
    private readonly ShardInfoService _shards;
    private readonly ModelRepository _repo;

    public Misc(BotConfig botConfig, IMetrics metrics, CpuStatService cpu, ModelRepository repo, ShardInfoService shards)
    {
        _botConfig = botConfig;
        _metrics = metrics;
        _cpu = cpu;
        _repo = repo;
        _shards = shards;
    }

    public async Task Invite(Context ctx)
    {
        var permissions =
            PermissionSet.AddReactions |
            PermissionSet.AttachFiles |
            PermissionSet.EmbedLinks |
            PermissionSet.ManageMessages |
            PermissionSet.ManageWebhooks |
            PermissionSet.ReadMessageHistory |
            PermissionSet.SendMessages;

        var invite =
            $"https://discord.com/oauth2/authorize?client_id={_botConfig.ClientId}&scope=bot%20applications.commands&permissions={(ulong)permissions}";

        var botName = _botConfig.IsBetaBot ? "PluralKit Beta" : "PluralKit";
        await ctx.Reply($"{Emojis.Success} Use this link to add {botName} to your server:\n<{invite}>");
    }

    public async Task Stats(Context ctx)
    {
        var timeBefore = SystemClock.Instance.GetCurrentInstant();
        var msg = await ctx.Reply("...");
        var timeAfter = SystemClock.Instance.GetCurrentInstant();
        var apiLatency = timeAfter - timeBefore;

        var process = Process.GetCurrentProcess();
        var stats = await GetStats(ctx);
        var shards = await _shards.GetShards();

        var shardInfo = shards.Where(s => s.ShardId == ctx.ShardId).FirstOrDefault();
        var shardsUp = shards.Where(s => s.Up).Count();

        if (stats == null)
        {
            var content = $"Stats unavailable (is scheduled_tasks service running?)\n\n**Quick info:**"
                        + $"\nPluralKit [{BuildInfoService.Version}](<https://github.com/pluralkit/pluralkit/commit/{BuildInfoService.FullVersion}>)"
                        // + (BuildInfoService.IsDev ? ", **development build**" : "")
                        + $"\nCurrently on shard {ctx.ShardId}, {shardsUp}/{shards.Count()} shards up,"
                        + $" API latency: {apiLatency.TotalMilliseconds:F0}ms";
            await ctx.Rest.EditMessage(msg.ChannelId, msg.Id,
                new MessageEditRequest { Content = content });
            return;
        }

        var embed = new EmbedBuilder();

        embed
            .Field(new("Connection status", $"**{shards.Count()}** shards across **{shards.Select(s => s.ClusterId).Distinct().Count()}** clusters (**{shardsUp} up**)\n"
                                            + $"Current server is on **shard {ctx.ShardId} (cluster {shardInfo.ClusterId ?? 0})**\n"
                                            + $"Latency: API **{apiLatency.TotalMilliseconds:F0}ms** (p90: {stats.prom.nirn_proxy_latency_p90 * 1000:F0}ms, p99: {stats.prom.nirn_proxy_latency_p99 * 1000:F0}ms), "
                                            + $"shard **{shardInfo.Latency}ms** (avg: {stats.prom.shard_latency_average}ms)", true))
            .Field(new("Resource usage", $"**CPU:** {stats.prom.cpu_used}% used / {stats.prom.cpu_total_cores} total cores ({stats.prom.cpu_total_threads} threads)\n"
                                        + $"**Memory:** {(stats.prom.memory_used / 1_000_000_000):N1}GB used / {(stats.prom.memory_total / 1_000_000_000):N1}GB total", true))
            .Field(new("Usage metrics", $"Messages received: **{stats.prom.messages_1m}/s** ({stats.prom.messages_15m}/s over 15m)\n" +
                                        $"Messages proxied: **{stats.prom.proxy_1m}/s** ({stats.prom.proxy_15m}/s over 15m, {stats.db.messages_24h:N0} total in last 24h)\n" +
                                        $"Commands executed: **{stats.prom.commands_1m}/m** ({stats.prom.commands_15m}/m over 15m)"));

        embed.Field(new("Total numbers", $"**{stats.db.systems:N0}** systems, **{stats.db.members:N0}** members, **{stats.db.groups:N0}** groups, "
                                       + $"**{stats.db.switches:N0}** switches, **{stats.db.messages:N0}** messages\n" +
                                         $"**{stats.db.guilds:N0}** servers with **{stats.db.channels:N0}** channels"));

        embed.Field(new("", Help.EmbedFooter));

        var uptime = ((DateTimeOffset)process.StartTime).ToUnixTimeSeconds();
        embed.Description($"### PluralKit [{BuildInfoService.Version}](https://github.com/pluralkit/pluralkit/commit/{BuildInfoService.FullVersion})\n" +
                          $"Built on <t:{BuildInfoService.Timestamp}> (<t:{BuildInfoService.Timestamp}:R>)"
                        // + (BuildInfoService.IsDev ? ", **development build**" : "")
                        + $"\nLast restart: <t:{uptime}:R>");

        await ctx.Rest.EditMessage(msg.ChannelId, msg.Id,
            new MessageEditRequest { Content = "", Embeds = new[] { embed.Build() } });
    }

    private async Task<Stats?> GetStats(Context ctx)
    {
        var db = ctx.Redis.Connection.GetDatabase();
        var data = await db.StringGetAsync("statsapi");
        return data.HasValue ? JsonConvert.DeserializeObject<Stats>(data) : null;
    }
}

// none of these fields are "assigned to" for some reason
#pragma warning disable CS0649

class Stats
{
    public DbStats db;
    public PrometheusStats prom;
};

class DbStats
{
    public double systems;
    public double members;
    public double groups;
    public double switches;
    public double messages;
    public double messages_24h;
    public double guilds;
    public double channels;
};

class PrometheusStats
{
    public double messages_1m;
    public double messages_15m;
    public double proxy_1m;
    public double proxy_15m;
    public double commands_1m;
    public double commands_15m;
    public double cpu_total_cores;
    public double cpu_total_threads;
    public double cpu_used;
    public double memory_total;
    public double memory_used;
    public double nirn_proxy_rps;
    public double nirn_proxy_latency_p90;
    public double nirn_proxy_latency_p99;
    public double shard_latency_average;
};