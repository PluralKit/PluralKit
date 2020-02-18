using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using App.Metrics;

using Discord;

using Humanizer;

using NodaTime;

using PluralKit.Core;

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
            var clientId = _botConfig.ClientId ?? (await ctx.Client.GetApplicationInfoAsync()).Id;
            var permissions = new GuildPermissions(
                addReactions: true,
                attachFiles: true,
                embedLinks: true,
                manageMessages: true,
                manageWebhooks: true,
                readMessageHistory: true,
                sendMessages: true
            );

            var invite = $"https://discordapp.com/oauth2/authorize?client_id={clientId}&scope=bot&permissions={permissions.RawValue}";
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
            var shardTotal = ctx.Client.Shards.Count;
            var shardUpTotal = ctx.Client.Shards.Select(s => s.ConnectionState == ConnectionState.Connected).Count();
            var shardInfo = _shards.GetShardInfo(ctx.Shard);
            
            var process = Process.GetCurrentProcess();
            var memoryUsage = process.WorkingSet64;

            var shardUptime = SystemClock.Instance.GetCurrentInstant() - shardInfo.LastConnectionTime;

            var embed = new EmbedBuilder()
                .AddField("Messages processed", $"{messagesReceived.OneMinuteRate * 60:F1}/m ({messagesReceived.FifteenMinuteRate * 60:F1}/m over 15m)", true)
                .AddField("Messages proxied", $"{messagesProxied.OneMinuteRate * 60:F1}/m ({messagesProxied.FifteenMinuteRate * 60:F1}/m over 15m)", true)
                .AddField("Commands executed", $"{commandsRun.OneMinuteRate * 60:F1}/m ({commandsRun.FifteenMinuteRate * 60:F1}/m over 15m)", true)
                .AddField("Current shard", $"Shard #{shardId} (of {shardTotal} total, {shardUpTotal} are up)", true)
                .AddField("Shard uptime", $"{DateTimeFormats.DurationFormat.Format(shardUptime)} ({shardInfo.DisconnectionCount} disconnections)", true)
                .AddField("CPU usage", $"{_cpu.LastCpuMeasure:P1}", true)
                .AddField("Memory usage", $"{memoryUsage / 1024 / 1024} MiB", true)
                .AddField("Latency", $"API: {(msg.Timestamp - ctx.Message.Timestamp).TotalMilliseconds:F0} ms, shard: {shardInfo.ShardLatency} ms", true)
                .AddField("Total numbers", $"{totalSystems:N0} systems, {totalMembers:N0} members, {totalSwitches:N0} switches, {totalMessages:N0} messages");

            if (await MiscUtils.EnsureEmbedPermissions(ctx, "send the stats card")) await msg.ModifyAsync(f =>
            {
                f.Content = "";
                f.Embed = embed.Build();
            });
        }
        
        public async Task PermCheckGuild(Context ctx)
        {
            IGuild guild;

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
                guild = ctx.Client.GetGuild(guildId);
                if (guild == null)
                    throw Errors.GuildNotFound(guildId);
            }

            var requiredPermissions = new []
            {
                ChannelPermission.ViewChannel,
                ChannelPermission.SendMessages,
                ChannelPermission.AddReactions,
                ChannelPermission.AttachFiles,
                ChannelPermission.EmbedLinks,
                ChannelPermission.ManageMessages,
                ChannelPermission.ManageWebhooks
            };

            // Loop through every channel and group them by sets of permissions missing
            var permissionsMissing = new Dictionary<ulong, List<ITextChannel>>();
            foreach (var channel in await guild.GetTextChannelsAsync())
            {
                // TODO: do we need to hide channels here to prevent info-leaking?
                var perms = await channel.PermissionsIn();

                // We use a bitfield so we can set individual permission bits in the loop
                ulong missingPermissionField = 0;
                foreach (var requiredPermission in requiredPermissions)
                    if (!perms.Has(requiredPermission))
                        missingPermissionField |= (ulong) requiredPermission;

                // If we're not missing any permissions, don't bother adding it to the dict
                // This means we can check if the dict is empty to see if all channels are proxyable
                if (missingPermissionField != 0)
                {
                    permissionsMissing.TryAdd(missingPermissionField, new List<ITextChannel>());
                    permissionsMissing[missingPermissionField].Add(channel);
                }
            }
            
            // Generate the output embed
            var eb = new EmbedBuilder()
                .WithTitle($"Permission check for **{guild.Name.SanitizeMentions()}**");

            if (permissionsMissing.Count == 0)
            {
                eb.WithDescription($"No errors found, all channels proxyable :)").WithColor(Color.Green);
            }
            else
            {
                foreach (var (missingPermissionField, channels) in permissionsMissing)
                {
                    // Each missing permission field can have multiple missing channels
                    // so we extract them all and generate a comma-separated list
                    var missingPermissionNames = string.Join(", ", new ChannelPermissions(missingPermissionField)
                        .ToList()
                        .Select(perm => perm.Humanize().Transform(To.TitleCase)));
                    
                    var channelsList = string.Join("\n", channels
                        .OrderBy(c => c.Position)
                        .Select(c => $"#{c.Name}"));
                    eb.AddField($"Missing *{missingPermissionNames}*", channelsList.Truncate(1000));
                    eb.WithColor(Color.Red);
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

            await ctx.Reply(embed: await _embeds.CreateMessageInfoEmbed(message));
        }
    }
}