using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using App.Metrics;

using Discord;

using Humanizer;

using NodaTime;

using PluralKit.Bot.CommandSystem;
using PluralKit.Core;

namespace PluralKit.Bot.Commands {
    public class MiscCommands
    {
        private BotConfig _botConfig;
        private IMetrics _metrics;
        private CpuStatService _cpu;
        private ShardInfoService _shards;

        public MiscCommands(BotConfig botConfig, IMetrics metrics, CpuStatService cpu, ShardInfoService shards)
        {
            _botConfig = botConfig;
            _metrics = metrics;
            _cpu = cpu;
            _shards = shards;
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
        
        public Task Mn(Context ctx) => ctx.Reply("Gotta catch 'em all!");
        public Task Fire(Context ctx) => ctx.Reply("*A giant lightning bolt promptly erupts into a pillar of fire as it hits your opponent.*");
        public Task Thunder(Context ctx) => ctx.Reply("*A giant ball of lightning is conjured and fired directly at your opponent, vanquishing them.*");
        public Task Freeze(Context ctx) => ctx.Reply("*A giant crystal ball of ice is charged and hurled toward your opponent, bursting open and freezing them solid on contact.*");
        public Task Starstorm(Context ctx) => ctx.Reply("*Vibrant colours burst forth from the sky as meteors rain down upon your opponent.*");
        public Task Flash(Context ctx) => ctx.Reply("*A ball of green light appears above your head and flies towards your enemy, exploding on contact.*");

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
                .AddField("Shard uptime", $"{Formats.DurationFormat.Format(shardUptime)} ({shardInfo.DisconnectionCount} disconnections)", true)
                .AddField("CPU usage", $"{_cpu.LastCpuMeasure * 100:P1}", true)
                .AddField("Memory usage", $"{memoryUsage / 1024 / 1024} MiB", true)
                .AddField("Latency", $"API: {(msg.Timestamp - ctx.Message.Timestamp).TotalMilliseconds:F0} ms, shard: {shardInfo.ShardLatency} ms", true)
                .AddField("Total numbers", $"{totalSystems} systems, {totalMembers} members, {totalSwitches} switches, {totalMessages} messages");

            await msg.ModifyAsync(f =>
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
    }
}