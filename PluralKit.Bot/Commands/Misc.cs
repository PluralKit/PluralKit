using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.WebSockets;

using App.Metrics;

using DSharpPlus;

using Humanizer;

using NodaTime;

using PluralKit.Core;
using DSharpPlus.Entities;

namespace PluralKit.Bot {
    public class Misc
    {
        private BotConfig _botConfig;
        private IMetrics _metrics;
        private CpuStatService _cpu;
        private ShardInfoService _shards;
        private IDataStore _data;
        private EmbedService _embeds;

        public Misc(BotConfig botConfig, IMetrics metrics, CpuStatService cpu, ShardInfoService shards, IDataStore data, EmbedService embeds)
        {
            _botConfig = botConfig;
            _metrics = metrics;
            _cpu = cpu;
            _shards = shards;
            _data = data;
            _embeds = embeds;
        }
        
        public async Task Invite(Context ctx)
        {
            var clientId = _botConfig.ClientId ?? ctx.Client.CurrentApplication.Id;
            var permissions = new Permissions()
                .Grant(Permissions.AddReactions)
                .Grant(Permissions.AttachFiles)
                .Grant(Permissions.EmbedLinks)
                .Grant(Permissions.ManageMessages)
                .Grant(Permissions.ManageWebhooks)
                .Grant(Permissions.ReadMessageHistory)
                .Grant(Permissions.SendMessages);
            var invite = $"https://discordapp.com/oauth2/authorize?client_id={clientId}&scope=bot&permissions={(long)permissions}";
            await ctx.Reply($"{Emojis.Success} Use this link to add PluralKit to your server:\n<{invite}>");
        }
        
        public async Task Stats(Context ctx)
        {
            var msg = await ctx.Reply($"...");
            
            var messagesReceived = _metrics.Snapshot.GetForContext("Bot").Meters.First(m => m.MultidimensionalName == BotMetrics.MessagesReceived.Name).Value;
            var messagesProxied = _metrics.Snapshot.GetForContext("Bot").Meters.First(m => m.MultidimensionalName == BotMetrics.MessagesProxied.Name).Value;
            var commandsRun = _metrics.Snapshot.GetForContext("Bot").Meters.First(m => m.MultidimensionalName == BotMetrics.CommandsRun.Name).Value;

            var totalSystems = _metrics.Snapshot.GetForContext("Application").Gauges.First(m => m.MultidimensionalName == CoreMetrics.SystemCount.Name).Value;
            var totalMembers = _metrics.Snapshot.GetForContext("Application").Gauges.First(m => m.MultidimensionalName == CoreMetrics.MemberCount.Name).Value;
            var totalSwitches = _metrics.Snapshot.GetForContext("Application").Gauges.First(m => m.MultidimensionalName == CoreMetrics.SwitchCount.Name).Value;
            var totalMessages = _metrics.Snapshot.GetForContext("Application").Gauges.First(m => m.MultidimensionalName == CoreMetrics.MessageCount.Name).Value;

            var shardId = ctx.Shard.ShardId;
            var shardTotal = ctx.Client.ShardClients.Count;
            var shardUpTotal = ctx.Client.ShardClients.Where(x => x.Value.IsConnected()).Count();
            var shardInfo = _shards.GetShardInfo(ctx.Shard);
            
            var process = Process.GetCurrentProcess();
            var memoryUsage = process.WorkingSet64;

            var shardUptime = SystemClock.Instance.GetCurrentInstant() - shardInfo.LastConnectionTime;

            var embed = new DiscordEmbedBuilder()
                .AddField("Messages processed", $"{messagesReceived.OneMinuteRate * 60:F1}/m ({messagesReceived.FifteenMinuteRate * 60:F1}/m over 15m)", true)
                .AddField("Messages proxied", $"{messagesProxied.OneMinuteRate * 60:F1}/m ({messagesProxied.FifteenMinuteRate * 60:F1}/m over 15m)", true)
                .AddField("Commands executed", $"{commandsRun.OneMinuteRate * 60:F1}/m ({commandsRun.FifteenMinuteRate * 60:F1}/m over 15m)", true)
                .AddField("Current shard", $"Shard #{shardId} (of {shardTotal} total, {shardUpTotal} are up)", true)
                .AddField("Shard uptime", $"{DateTimeFormats.DurationFormat.Format(shardUptime)} ({shardInfo.DisconnectionCount} disconnections)", true)
                .AddField("CPU usage", $"{_cpu.LastCpuMeasure:P1}", true)
                .AddField("Memory usage", $"{memoryUsage / 1024 / 1024} MiB", true)
                .AddField("Latency", $"API: {(msg.Timestamp - ctx.Message.Timestamp).TotalMilliseconds:F0} ms, shard: {shardInfo.ShardLatency} ms", true)
                .AddField("Total numbers", $"{totalSystems:N0} systems, {totalMembers:N0} members, {totalSwitches:N0} switches, {totalMessages:N0} messages");
            await msg.ModifyAsync("", embed.Build());
        }

        public async Task PermCheckGuild(Context ctx)
        {
            DiscordGuild guild;

            if (ctx.Guild != null && !ctx.HasNext())
            {
                guild = ctx.Guild;
            }
            else
            {
                var guildIdStr = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a server ID or run this command as .");
                if (!ulong.TryParse(guildIdStr, out var guildId))
                    throw new PKSyntaxError($"Could not parse `{guildIdStr.SanitizeMentions()}` as an ID.");

                // TODO: will this call break for sharding if you try to request a guild on a different bot instance?
                guild = await ctx.Rest.GetGuildAsync(guildId);
                if (guild == null)
                    throw Errors.GuildNotFound(guildId);
            }
            
            var requiredPermissions = new []
            {
                Permissions.AccessChannels,
                Permissions.SendMessages,
                Permissions.AddReactions,
                Permissions.AttachFiles,
                Permissions.EmbedLinks,
                Permissions.ManageMessages,
                Permissions.ManageWebhooks
            };

            // Loop through every channel and group them by sets of permissions missing
            var permissionsMissing = new Dictionary<ulong, List<DiscordChannel>>();
            var guildTextChannels = (await guild.GetChannelsAsync()).Where(x => x.Type == ChannelType.Text);
            foreach (var channel in guildTextChannels)
            {
                // TODO: do we need to hide channels here to prevent info-leaking?
                var perms = await channel.PermissionsIn(ctx.Client.CurrentUser);

                // We use a bitfield so we can set individual permission bits in the loop
                ulong missingPermissionField = 0;
                foreach (var requiredPermission in requiredPermissions)
                    if (!perms.HasPermission(requiredPermission))
                        missingPermissionField |= (ulong) requiredPermission;

                // If we're not missing any permissions, don't bother adding it to the dict
                // This means we can check if the dict is empty to see if all channels are proxyable
                if (missingPermissionField != 0)
                {
                    permissionsMissing.TryAdd(missingPermissionField, new List<DiscordChannel>());
                    permissionsMissing[missingPermissionField].Add(channel);
                }
            }
            
            // Generate the output embed
            var eb = new DiscordEmbedBuilder()
                .WithTitle($"Permission check for **{guild.Name.SanitizeMentions()}**");

            if (permissionsMissing.Count == 0)
            {
                eb.WithDescription($"No errors found, all channels proxyable :)").WithColor(DiscordUtils.Green);
            }
            else
            {
                foreach (var (missingPermissionField, channels) in permissionsMissing)
                {
                    // Each missing permission field can have multiple missing channels
                    // so we extract them all and generate a comma-separated list
                    var missingPermissionNames = ((Permissions)missingPermissionField).ToPermissionString();
                    
                    var channelsList = string.Join("\n", channels
                        .OrderBy(c => c.Position)
                        .Select(c => $"#{c.Name}"));
                    eb.AddField($"Missing *{missingPermissionNames}*", channelsList.Truncate(1000));
                    eb.WithColor(DiscordUtils.Red);
                }
            }

            // Send! :)
            await ctx.Reply(embed: eb.Build());
        }
        
        public async Task GetMessage(Context ctx)
        {
            var word = ctx.PopArgument() ?? throw new PKSyntaxError("You must pass a message ID or link.");

            ulong messageId;
            if (ulong.TryParse(word, out var id))
                messageId = id;
            else if (Regex.Match(word, "https://discordapp.com/channels/\\d+/\\d+/(\\d+)") is Match match && match.Success)
                messageId = ulong.Parse(match.Groups[1].Value);
            else throw new PKSyntaxError($"Could not parse `{word}` as a message ID or link.");

            var message = await _data.GetMessage(messageId);
            if (message == null) throw Errors.MessageNotFound(messageId);

            await ctx.Reply(embed: await _embeds.CreateMessageInfoEmbed(ctx.Shard, message));
        }
    }
}