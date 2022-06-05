using System.Diagnostics;

using App.Metrics;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Misc
{
    private readonly BotConfig _botConfig;
    private readonly IDiscordCache _cache;
    private readonly CpuStatService _cpu;
    private readonly IMetrics _metrics;
    private readonly ShardInfoService _shards;
    private readonly ModelRepository _repo;

    public Misc(BotConfig botConfig, IMetrics metrics, CpuStatService cpu, ModelRepository repo, ShardInfoService shards, IDiscordCache cache)
    {
        _botConfig = botConfig;
        _metrics = metrics;
        _cpu = cpu;
        _repo = repo;
        _shards = shards;
        _cache = cache;
    }

    public async Task Invite(Context ctx)
    {
        var clientId = _botConfig.ClientId ?? await _cache.GetOwnUser();

        var permissions =
            PermissionSet.AddReactions |
            PermissionSet.AttachFiles |
            PermissionSet.EmbedLinks |
            PermissionSet.ManageMessages |
            PermissionSet.ManageWebhooks |
            PermissionSet.ReadMessageHistory |
            PermissionSet.SendMessages;

        var invite =
            $"https://discord.com/oauth2/authorize?client_id={clientId}&scope=bot%20applications.commands&permissions={(ulong)permissions}";

        var botName = _botConfig.IsBetaBot ? "PluralKit Beta" : "PluralKit";
        await ctx.Reply($"{Emojis.Success} Use this link to add {botName} to your server:\n<{invite}>");
    }

    public async Task Stats(Context ctx)
    {
        var timeBefore = SystemClock.Instance.GetCurrentInstant();
        var msg = await ctx.Reply("...");
        var timeAfter = SystemClock.Instance.GetCurrentInstant();
        var apiLatency = timeAfter - timeBefore;

        var embed = new EmbedBuilder();

        // todo: these will be inaccurate when the bot is actually multi-process

        var messagesReceived = _metrics.Snapshot.GetForContext("Bot").Meters
            .FirstOrDefault(m => m.MultidimensionalName == BotMetrics.MessagesReceived.Name)?.Value;
        if (messagesReceived != null)
            embed.Field(new Embed.Field("Messages processed",
                $"{messagesReceived.OneMinuteRate * 60:F1}/m ({messagesReceived.FifteenMinuteRate * 60:F1}/m over 15m)",
                true));

        var messagesProxied = _metrics.Snapshot.GetForContext("Bot").Meters
            .FirstOrDefault(m => m.MultidimensionalName == BotMetrics.MessagesProxied.Name)?.Value;
        if (messagesProxied != null)
            embed.Field(new Embed.Field("Messages proxied",
                $"{messagesProxied.OneMinuteRate * 60:F1}/m ({messagesProxied.FifteenMinuteRate * 60:F1}/m over 15m)",
                true));

        var commandsRun = _metrics.Snapshot.GetForContext("Bot").Meters
            .FirstOrDefault(m => m.MultidimensionalName == BotMetrics.CommandsRun.Name)?.Value;
        if (commandsRun != null)
            embed.Field(new Embed.Field("Commands executed",
                $"{commandsRun.OneMinuteRate * 60:F1}/m ({commandsRun.FifteenMinuteRate * 60:F1}/m over 15m)",
                true));

        var isCluster = _botConfig.Cluster != null && _botConfig.Cluster.TotalShards != ctx.Cluster.Shards.Count;

        var counts = await _repo.GetStats();
        var shards = await _shards.GetShards();

        var shardInfo = shards.Where(s => s.ShardId == ctx.ShardId).FirstOrDefault();

        // todo: if we're running multiple processes, it is not useful to get the CPU/RAM usage of just the current one
        var process = Process.GetCurrentProcess();
        var memoryUsage = process.WorkingSet64;

        var now = SystemClock.Instance.GetCurrentInstant().ToUnixTimeSeconds();
        var shardUptime = Duration.FromSeconds(now - shardInfo?.LastConnection ?? 0);

        var shardTotal = _botConfig.Cluster?.TotalShards ?? shards.Count();
        int shardClusterTotal = ctx.Cluster.Shards.Count;
        var shardUpTotal = shards.Where(x => x.Up).Count();

        embed
            .Field(new Embed.Field("Current shard",
                $"Shard #{ctx.ShardId} (of {shardTotal} total,"
                    + (isCluster ? $" {shardClusterTotal} in this cluster," : "") + $" {shardUpTotal} are up)"
                , true))
            .Field(new Embed.Field("Shard uptime",
                $"{shardUptime.FormatDuration()} ({shardInfo?.DisconnectionCount} disconnections)", true))
            .Field(new Embed.Field("CPU usage", $"{_cpu.LastCpuMeasure:P1}", true))
            .Field(new Embed.Field("Memory usage", $"{memoryUsage / 1024 / 1024} MiB", true))
            .Field(new Embed.Field("Latency",
                $"API: {apiLatency.TotalMilliseconds:F0} ms, shard: {shardInfo?.Latency} ms",
                true));

        embed.Field(new("Total numbers", $" {counts.SystemCount:N0} systems,"
                                       + $" {counts.MemberCount:N0} members,"
                                       + $" {counts.GroupCount:N0} groups,"
                                       + $" {counts.SwitchCount:N0} switches,"
                                       + $" {counts.MessageCount:N0} messages"));

        embed
            .Footer(new(String.Join(" \u2022 ", new[] {
                $"PluralKit {BuildInfoService.Version}",
                (isCluster ? $"Cluster {_botConfig.Cluster.NodeIndex}" : ""),
                "https://github.com/PluralKit/PluralKit",
                "Last restarted:",
            })))
            .Timestamp(process.StartTime.ToString("O"));

        await ctx.Rest.EditMessage(msg.ChannelId, msg.Id,
            new MessageEditRequest { Content = "", Embeds = new[] { embed.Build() } });
    }
}