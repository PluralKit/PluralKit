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
        private readonly ProxyService _proxy;
        private readonly ProxyMatcher _matcher;

        public Misc(BotConfig botConfig, IMetrics metrics, CpuStatService cpu, ShardInfoService shards, EmbedService embeds, ModelRepository repo, IDatabase db, IDiscordCache cache, DiscordApiClient rest, Bot bot, Cluster cluster, ProxyService proxy, ProxyMatcher matcher)
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

            var totalSystems = _metrics.Snapshot.GetForContext("Application").Gauges.FirstOrDefault(m => m.MultidimensionalName == CoreMetrics.SystemCount.Name)?.Value ?? 0;
            var totalMembers = _metrics.Snapshot.GetForContext("Application").Gauges.FirstOrDefault(m => m.MultidimensionalName == CoreMetrics.MemberCount.Name)?.Value ?? 0;
            var totalGroups = _metrics.Snapshot.GetForContext("Application").Gauges.FirstOrDefault(m => m.MultidimensionalName == CoreMetrics.GroupCount.Name)?.Value ?? 0;
            var totalSwitches = _metrics.Snapshot.GetForContext("Application").Gauges.FirstOrDefault(m => m.MultidimensionalName == CoreMetrics.SwitchCount.Name)?.Value ?? 0;
            var totalMessages = _metrics.Snapshot.GetForContext("Application").Gauges.FirstOrDefault(m => m.MultidimensionalName == CoreMetrics.MessageCount.Name)?.Value ?? 0;

            var shardId = ctx.Shard.ShardId;
            var shardTotal = ctx.Cluster.Shards.Count;
            var shardUpTotal = _shards.Shards.Where(x => x.Connected).Count();
            var shardInfo = _shards.GetShardInfo(ctx.Shard);
            
            var process = Process.GetCurrentProcess();
            var memoryUsage = process.WorkingSet64;

            var now = SystemClock.Instance.GetCurrentInstant();
            var shardUptime = now - shardInfo.LastConnectionTime;

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
                .Field(new("Total numbers", $"{totalSystems:N0} systems, {totalMembers:N0} members, {totalGroups:N0} groups, {totalSwitches:N0} switches, {totalMessages:N0} messages"))
                .Timestamp(now.ToDateTimeOffset().ToString("O"))
                .Footer(new($"PluralKit {BuildInfoService.Version} • https://github.com/xSke/PluralKit"));;
            await ctx.Rest.EditMessage(msg.ChannelId, msg.Id,
                new MessageEditRequest {Content = "", Embed = embed.Build()});
        }

        public async Task PermCheckGuild(Context ctx)
        {
            Guild guild;
            GuildMemberPartial senderGuildUser = null;

            if (ctx.Guild != null && !ctx.HasNext())
            {
                guild = ctx.Guild;
                senderGuildUser = ctx.Member;
            }
            else
            {
                var guildIdStr = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a server ID or run this command in a server.");
                if (!ulong.TryParse(guildIdStr, out var guildId))
                    throw new PKSyntaxError($"Could not parse {guildIdStr.AsCode()} as an ID.");

                try {
                    guild = await _rest.GetGuild(guildId);
                } catch (Myriad.Rest.Exceptions.ForbiddenException) {
                    throw Errors.GuildNotFound(guildId);
                }
                
                if (guild != null) 
                    senderGuildUser = await _rest.GetGuildMember(guildId, ctx.Author.Id);
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
                var userPermissions = PermissionExtensions.PermissionsFor(guild, channel, ctx.Author.Id, senderGuildUser);
                
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
            var messageId = ctx.MatchMessage(true);
            if (messageId == null)
            {
                if (!ctx.HasNext())
                    throw new PKSyntaxError("You must pass a message ID or link.");
                throw new PKSyntaxError($"Could not parse {ctx.PeekArgument().AsCode()} as a message ID or link.");
            }
            
            var message = await _db.Execute(c => _repo.GetMessage(c, messageId.Value));
            if (message == null) throw Errors.MessageNotFound(messageId.Value);

            if (ctx.Match("delete") || ctx.MatchFlag("delete"))
            {
                if (message.System.Id != ctx.System.Id)
                    throw new PKError("You can only delete your own messages.");
                await ctx.Rest.DeleteMessage(message.Message.Channel, message.Message.Mid);
                await ctx.Rest.DeleteMessage(ctx.Message);
                return;
            }
            if (ctx.Match("author") || ctx.MatchFlag("author"))
            {
                var user = await _cache.GetOrFetchUser(_rest, message.Message.Sender);
                var eb = new EmbedBuilder()
                    .Author(new(user != null ? $"{user.Username}#{user.Discriminator}" : $"Deleted user ${message.Message.Sender}", IconUrl: user != null ? user.AvatarUrl() : null))
                    .Description(message.Message.Sender.ToString());

                await ctx.Reply(user != null ? $"{user.Mention()} ({user.Id})" : $"*(deleted user {message.Message.Sender})*", embed: eb.Build());
                return;
            }

            await ctx.Reply(embed: await _embeds.CreateMessageInfoEmbed(message));
        }

        public async Task MessageProxyCheck(Context ctx)
        {
            ulong channelId;
            ulong messageId;

            // if the message isn't a reply, check for a message link
            if (ctx.Message.Type != Message.MessageType.Reply || ctx.Message.MessageReference == null)
            {
                if (!ctx.HasNext())
                    throw new PKSyntaxError("You must reply to a message or pass a message link.");

                var word = ctx.PeekArgument();
                var match = Regex.Match(word, "https://(?:\\w+.)?discord(?:app)?.com/channels/\\d+/(\\d+)/(\\d+)");
                if (!match.Success)
                    throw new PKError($"Could not parse {ctx.PeekArgument().AsCode()} as a message link.");
                    
                channelId = ulong.Parse(match.Groups[1].Value);
                messageId = ulong.Parse(match.Groups[2].Value);
            } else
            // message is a reply, use it's values instead
            {
                channelId = ctx.Message.MessageReference.ChannelId.Value;
                messageId = ctx.Message.MessageReference.MessageId.Value;
            }
            
            // get the message info
            var msg = ctx.Message;
            try 
            {
                msg = await _rest.GetMessage(channelId, messageId);
            }
            catch (ForbiddenException)
            {
                throw new PKError("Unable to get the message provided.");
            }

            if (msg == null) 
                throw new PKError("The message you are checking does not exist.");

            // check if the message is not a webhook and sent by the same user as the message that's being checked
            if (msg.WebhookId != null)
                throw new PKError("You cannot check messages sent by a webhook.");
            if (msg.Author.Id != ctx.Author.Id)
                throw new PKError("You can only check your own messages.");


            // get the channel info
            var channel = await _rest.GetChannel(msg.ChannelId);

            MessageContext context;
            await using (var conn = await _db.Obtain())
            using (_metrics.Measure.Timer.Time(BotMetrics.MessageContextQueryTime))
                // using the channel.GuildId here since _rest.GetMessage() doesn't return the GuildId
                context = await _repo.GetMessageContext(conn, msg.Author.Id, channel.GuildId.Value, msg.ChannelId);

            List<ProxyMember> members;
            await using (var conn = await _db.Obtain())
            using (_metrics.Measure.Timer.Time(BotMetrics.ProxyMembersQueryTime))
                members = (await _repo.GetProxyMembers(conn, msg.Author.Id, channel.GuildId.Value)).ToList();

            // Run everything through the checks, catch the ProxyCheckFailedException, and reply with it's message.
            try 
            { 
                _proxy.ShouldProxy(channel, msg, context);
                _matcher.TryMatch(context, members, out var match, msg.Content, msg.Attachments.Length > 0, context.AllowAutoproxy);

        
                await ctx.Reply("This message has not been proxied for unknown reasons, sorry about that!");
            } catch (ProxyService.ProxyChecksFailedException e)
            {
                await ctx.Reply($"{e.Message}");
            }
        }
    }
}
