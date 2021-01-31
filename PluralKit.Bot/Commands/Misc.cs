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
using Myriad.Rest.Types.Requests;
using Myriad.Types;

namespace PluralKit.Bot {
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

        public Misc(BotConfig botConfig, IMetrics metrics, CpuStatService cpu, ShardInfoService shards, EmbedService embeds, ModelRepository repo, IDatabase db, IDiscordCache cache, DiscordApiClient rest, Bot bot, Cluster cluster)
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

            var totalSystems = _metrics.Snapshot.GetForContext("Application").Gauges.FirstOrDefault(m => m.MultidimensionalName == CoreMetrics.SystemCount.Name)?.Value ?? 0;
            var totalMembers = _metrics.Snapshot.GetForContext("Application").Gauges.FirstOrDefault(m => m.MultidimensionalName == CoreMetrics.MemberCount.Name)?.Value ?? 0;
            var totalGroups = _metrics.Snapshot.GetForContext("Application").Gauges.FirstOrDefault(m => m.MultidimensionalName == CoreMetrics.GroupCount.Name)?.Value ?? 0;
            var totalSwitches = _metrics.Snapshot.GetForContext("Application").Gauges.FirstOrDefault(m => m.MultidimensionalName == CoreMetrics.SwitchCount.Name)?.Value ?? 0;
            var totalMessages = _metrics.Snapshot.GetForContext("Application").Gauges.FirstOrDefault(m => m.MultidimensionalName == CoreMetrics.MessageCount.Name)?.Value ?? 0;

            var shardId = ctx.ShardNew.ShardInfo.ShardId;
            var shardTotal = ctx.Cluster.Shards.Count;
            var shardUpTotal = _shards.Shards.Where(x => x.Connected).Count();
            var shardInfo = _shards.GetShardInfo(ctx.ShardNew);
            
            var process = Process.GetCurrentProcess();
            var memoryUsage = process.WorkingSet64;

            var shardUptime = SystemClock.Instance.GetCurrentInstant() - shardInfo.LastConnectionTime;

            var embed = new EmbedBuilder();
            if (messagesReceived != null) embed.Field(new("Messages processed",$"{messagesReceived.OneMinuteRate * 60:F1}/m ({messagesReceived.FifteenMinuteRate * 60:F1}/m over 15m)", true));
            if (messagesProxied != null) embed.Field(new("Messages proxied", $"{messagesProxied.OneMinuteRate * 60:F1}/m ({messagesProxied.FifteenMinuteRate * 60:F1}/m over 15m)", true));
            if (commandsRun != null) embed.Field(new("Commands executed", $"{commandsRun.OneMinuteRate * 60:F1}/m ({commandsRun.FifteenMinuteRate * 60:F1}/m over 15m)", true));

            embed
                .Field(new("Current shard", $"Shard #{shardId} (of {shardTotal} total, {shardUpTotal} are up)", true))
                .Field(new("Shard uptime", $"{shardUptime.FormatDuration()} ({shardInfo.DisconnectionCount} disconnections)", true))
                .Field(new("CPU usage", $"{_cpu.LastCpuMeasure:P1}", true))
                .Field(new("Memory usage", $"{memoryUsage / 1024 / 1024} MiB", true))
                .Field(new("Latency", $"API: {apiLatency.TotalMilliseconds:F0} ms, shard: {shardInfo.ShardLatency.Milliseconds} ms", true))
                .Field(new("Total numbers", $"{totalSystems:N0} systems, {totalMembers:N0} members, {totalGroups:N0} groups, {totalSwitches:N0} switches, {totalMessages:N0} messages"));
            await ctx.RestNew.EditMessage(msg.ChannelId, msg.Id,
                new MessageEditRequest {Content = "", Embed = embed.Build()});
        }

        public async Task PermCheckGuild(Context ctx)
        {
            Guild guild;
            GuildMemberPartial senderGuildUser = null;

            if (ctx.GuildNew != null && !ctx.HasNext())
            {
                guild = ctx.GuildNew;
                senderGuildUser = ctx.MemberNew;
            }
            else
            {
                var guildIdStr = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a server ID or run this command in a server.");
                if (!ulong.TryParse(guildIdStr, out var guildId))
                    throw new PKSyntaxError($"Could not parse {guildIdStr.AsCode()} as an ID.");

                guild = await _rest.GetGuild(guildId);
                if (guild != null) 
                    senderGuildUser = await _rest.GetGuildMember(guildId, ctx.AuthorNew.Id);
                if (guild == null || senderGuildUser == null) 
                    throw Errors.GuildNotFound(guildId);
            }

            var requiredPermissions = new []
            {
                PermissionSet.ViewChannel,
                PermissionSet.SendMessages,
                PermissionSet.AddReactions,
                PermissionSet.AttachFiles,
                PermissionSet.EmbedLinks,
                PermissionSet.ManageMessages,
                PermissionSet.ManageWebhooks
            };

            // Loop through every channel and group them by sets of permissions missing
            var permissionsMissing = new Dictionary<ulong, List<Channel>>();
            var hiddenChannels = 0;
            foreach (var channel in await _rest.GetGuildChannels(guild.Id))
            {
                var botPermissions = _bot.PermissionsIn(channel.Id);
                var userPermissions = PermissionExtensions.PermissionsFor(guild, channel, ctx.AuthorNew.Id, senderGuildUser.Roles);
                
                if ((userPermissions & PermissionSet.ViewChannel) == 0)
                {
                    // If the user can't see this channel, don't calculate permissions for it
                    // (to prevent info-leaking, mostly)
                    // Instead, count how many hidden channels and show the user (so they don't get confused)
                    hiddenChannels++;
                    continue;
                }

                // We use a bitfield so we can set individual permission bits in the loop
                // TODO: Rewrite with proper bitfield math
                ulong missingPermissionField = 0;
                foreach (var requiredPermission in requiredPermissions)
                    if ((botPermissions & requiredPermission) == 0)
                        missingPermissionField |= (ulong) requiredPermission;

                // If we're not missing any permissions, don't bother adding it to the dict
                // This means we can check if the dict is empty to see if all channels are proxyable
                if (missingPermissionField != 0)
                {
                    permissionsMissing.TryAdd(missingPermissionField, new List<Channel>());
                    permissionsMissing[missingPermissionField].Add(channel);
                }
            }
            
            // Generate the output embed
            var eb = new EmbedBuilder()
                .Title($"Permission check for **{guild.Name}**");

            if (permissionsMissing.Count == 0)
            {
                eb.Description($"No errors found, all channels proxyable :)").Color(DiscordUtils.Green);
            }
            else
            {
                foreach (var (missingPermissionField, channels) in permissionsMissing)
                {
                    // Each missing permission field can have multiple missing channels
                    // so we extract them all and generate a comma-separated list
                    var missingPermissionNames = ((PermissionSet) missingPermissionField).ToPermissionString();
                    
                    var channelsList = string.Join("\n", channels
                        .OrderBy(c => c.Position)
                        .Select(c => $"#{c.Name}"));
                    eb.Field(new($"Missing *{missingPermissionNames}*", channelsList.Truncate(1000)));
                    eb.Color(DiscordUtils.Red);
                }
            }

            if (hiddenChannels > 0)
                eb.Footer(new($"{"channel".ToQuantity(hiddenChannels)} were ignored as you do not have view access to them."));

            // Send! :)
            await ctx.Reply(embed: eb.Build());
        }
        
        public async Task GetMessage(Context ctx)
        {
            var word = ctx.PopArgument() ?? throw new PKSyntaxError("You must pass a message ID or link.");

            ulong messageId;
            if (ulong.TryParse(word, out var id))
                messageId = id;
            else if (Regex.Match(word, "https://discord(?:app)?.com/channels/\\d+/\\d+/(\\d+)") is Match match && match.Success)
                messageId = ulong.Parse(match.Groups[1].Value);
            else throw new PKSyntaxError($"Could not parse {word.AsCode()} as a message ID or link.");

            var message = await _db.Execute(c => _repo.GetMessage(c, messageId));
            if (message == null) throw Errors.MessageNotFound(messageId);

            await ctx.Reply(embed: await _embeds.CreateMessageInfoEmbed(message));
        }
    }
}