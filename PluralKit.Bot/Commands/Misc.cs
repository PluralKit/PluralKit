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
    private readonly ModelRepository _repo;
    private readonly ShardInfoService _shards;

    public Misc(BotConfig botConfig, IMetrics metrics, CpuStatService cpu, ShardInfoService shards,
                                                            ModelRepository repo, IDiscordCache cache)
    {
        _botConfig = botConfig;
        _metrics = metrics;
        _cpu = cpu;
        _shards = shards;
        _repo = repo;
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

        var messagesReceived = _metrics.Snapshot.GetForContext("Bot").Meters
            .FirstOrDefault(m => m.MultidimensionalName == BotMetrics.MessagesReceived.Name)?.Value;
        var messagesProxied = _metrics.Snapshot.GetForContext("Bot").Meters
            .FirstOrDefault(m => m.MultidimensionalName == BotMetrics.MessagesProxied.Name)?.Value;
        var commandsRun = _metrics.Snapshot.GetForContext("Bot").Meters
            .FirstOrDefault(m => m.MultidimensionalName == BotMetrics.CommandsRun.Name)?.Value;

        var counts = await _repo.GetStats();

        var shardId = ctx.Shard.ShardId;
        var shardTotal = ctx.Cluster.Shards.Count;
        var shardUpTotal = _shards.Shards.Where(x => x.Connected).Count();
        var shardInfo = _shards.GetShardInfo(ctx.Shard);

        var process = Process.GetCurrentProcess();
        var memoryUsage = process.WorkingSet64;

        var now = SystemClock.Instance.GetCurrentInstant();
        var shardUptime = now - shardInfo.LastConnectionTime;

        var embed = new EmbedBuilder();
        if (messagesReceived != null)
            embed.Field(new Embed.Field("Messages processed",
                $"{messagesReceived.OneMinuteRate * 60:F1}/m ({messagesReceived.FifteenMinuteRate * 60:F1}/m over 15m)",
                true));
        if (messagesProxied != null)
            embed.Field(new Embed.Field("Messages proxied",
                $"{messagesProxied.OneMinuteRate * 60:F1}/m ({messagesProxied.FifteenMinuteRate * 60:F1}/m over 15m)",
                true));
        if (commandsRun != null)
            embed.Field(new Embed.Field("Commands executed",
                $"{commandsRun.OneMinuteRate * 60:F1}/m ({commandsRun.FifteenMinuteRate * 60:F1}/m over 15m)",
                true));

        embed
            .Field(new Embed.Field("Current shard",
                $"Shard #{shardId} (of {shardTotal} total, {shardUpTotal} are up)", true))
            .Field(new Embed.Field("Shard uptime",
                $"{shardUptime.FormatDuration()} ({shardInfo.DisconnectionCount} disconnections)", true))
            .Field(new Embed.Field("CPU usage", $"{_cpu.LastCpuMeasure:P1}", true))
            .Field(new Embed.Field("Memory usage", $"{memoryUsage / 1024 / 1024} MiB", true))
            .Field(new Embed.Field("Latency",
                $"API: {apiLatency.TotalMilliseconds:F0} ms, shard: {shardInfo.ShardLatency.Milliseconds} ms",
                true))
            .Field(new Embed.Field("Total numbers", $" {counts.SystemCount:N0} systems,"
                                                  + $" {counts.MemberCount:N0} members,"
                                                  + $" {counts.GroupCount:N0} groups,"
                                                  + $" {counts.SwitchCount:N0} switches,"
                                                  + $" {counts.MessageCount:N0} messages"))
            .Timestamp(process.StartTime.ToString("O"))
            .Footer(new Embed.EmbedFooter(
                $"PluralKit {BuildInfoService.Version} • https://github.com/xSke/PluralKit • Last restarted: "));
        ;
        await ctx.Rest.EditMessage(msg.ChannelId, msg.Id,
            new MessageEditRequest { Content = "", Embed = embed.Build() });
    }
}