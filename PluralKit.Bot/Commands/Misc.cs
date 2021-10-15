using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using App.Metrics;

using Humanizer;

using NodaTime;

using PluralKit.Core;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

namespace PluralKit.Bot
{
    public class Misc
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
        private readonly ProxyService _proxy;
        private readonly ProxyMatcher _matcher;

        public Misc(BotConfig botConfig, IMetrics metrics, CpuStatService cpu, ShardInfoService shards, EmbedService embeds, ModelRepository repo,
                                IDatabase db, IDiscordCache cache, DiscordApiClient rest, Bot bot, Cluster cluster, ProxyService proxy, ProxyMatcher matcher)
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
            _proxy = proxy;
            _matcher = matcher;
        }

        public async Task Invite(Context ctx)
        {
            var clientId = _botConfig.ClientId ?? _cluster.Application?.Id;

            var permissions =
                PermissionSet.AddReactions |
                PermissionSet.AttachFiles |
                PermissionSet.EmbedLinks |
                PermissionSet.ManageMessages |
                PermissionSet.ManageWebhooks |
                PermissionSet.ReadMessageHistory |
                PermissionSet.SendMessages;

            var invite = $"https://discord.com/oauth2/authorize?client_id={clientId}&scope=bot%20applications.commands&permissions={(ulong)permissions}";
            await ctx.Reply($"{Emojis.Success} Use this link to add PluralKit to your server:\n<{invite}>");
        }

        public async Task Stats(Context ctx)
        {
            var timeBefore = SystemClock.Instance.GetCurrentInstant();
            var msg = await ctx.Reply($"...");
            var timeAfter = SystemClock.Instance.GetCurrentInstant();
            var apiLatency = timeAfter - timeBefore;

            var messagesReceived = _metrics.Snapshot.GetForContext("Bot").Meters.FirstOrDefault(m => m.MultidimensionalName == BotMetrics.MessagesReceived.Name)?.Value;
            var messagesProxied = _metrics.Snapshot.GetForContext("Bot").Meters.FirstOrDefault(m => m.MultidimensionalName == BotMetrics.MessagesProxied.Name)?.Value;
            var commandsRun = _metrics.Snapshot.GetForContext("Bot").Meters.FirstOrDefault(m => m.MultidimensionalName == BotMetrics.CommandsRun.Name)?.Value;

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
            if (messagesReceived != null) embed.Field(new("Messages processed", $"{messagesReceived.OneMinuteRate * 60:F1}/m ({messagesReceived.FifteenMinuteRate * 60:F1}/m over 15m)", true));
            if (messagesProxied != null) embed.Field(new("Messages proxied", $"{messagesProxied.OneMinuteRate * 60:F1}/m ({messagesProxied.FifteenMinuteRate * 60:F1}/m over 15m)", true));
            if (commandsRun != null) embed.Field(new("Commands executed", $"{commandsRun.OneMinuteRate * 60:F1}/m ({commandsRun.FifteenMinuteRate * 60:F1}/m over 15m)", true));

            embed
                .Field(new("Current shard", $"Shard #{shardId} (of {shardTotal} total, {shardUpTotal} are up)", true))
                .Field(new("Shard uptime", $"{shardUptime.FormatDuration()} ({shardInfo.DisconnectionCount} disconnections)", true))
                .Field(new("CPU usage", $"{_cpu.LastCpuMeasure:P1}", true))
                .Field(new("Memory usage", $"{memoryUsage / 1024 / 1024} MiB", true))
                .Field(new("Latency", $"API: {apiLatency.TotalMilliseconds:F0} ms, shard: {shardInfo.ShardLatency.Milliseconds} ms", true))
                .Field(new("Total numbers", $"{counts.SystemCount:N0} systems,"
                    + $" {counts.MemberCount:N0} members,"
                    + $" {counts.GroupCount:N0} groups,"
                    + $" {counts.SwitchCount:N0} switches,"
                    + $" {counts.MessageCount:N0} messages"))
                .Timestamp(Process.GetCurrentProcess().StartTime.ToString("O"))
                .Footer(new($"PluralKit {BuildInfoService.Version} • https://github.com/xSke/PluralKit • Last restarted: ")); ;
            await ctx.Rest.EditMessage(msg.ChannelId, msg.Id,
                new MessageEditRequest { Content = "", Embed = embed.Build() });
        }

    }
}