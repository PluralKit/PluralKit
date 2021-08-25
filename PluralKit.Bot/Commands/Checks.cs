using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Humanizer;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Checks
    {
        private readonly DiscordApiClient _rest;
        private readonly Bot _bot;
        private readonly IDiscordCache _cache;
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly BotConfig _botConfig;
        private readonly ProxyService _proxy;
        private readonly ProxyMatcher _matcher;

        public Checks(DiscordApiClient rest, Bot bot, IDiscordCache cache, IDatabase db, ModelRepository repo,
                      BotConfig botConfig, ProxyService proxy, ProxyMatcher matcher)
        {
            _rest = rest;
            _bot = bot;
            _cache = cache;
            _db = db;
            _repo = repo;
            _botConfig = botConfig;
            _proxy = proxy;
            _matcher = matcher;
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
                var webhookPermissions = _cache.EveryonePermissions(channel);
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
                
                if ((webhookPermissions & PermissionSet.UseExternalEmojis) == 0)
                    missingPermissionField |= (ulong) PermissionSet.UseExternalEmojis;

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

                    if (missingPermissionField == (ulong) PermissionSet.UseExternalEmojis)
                        eb.Footer(new($"Use External Emojis permissions must be granted to the @everyone role / Default Permissions."));
                    
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

        public async Task MessageProxyCheck(Context ctx)
        {
            if (!ctx.HasNext() && ctx.Message.MessageReference == null)
                throw new PKError("You need to specify a message.");
            
            var failedToGetMessage = "Could not find a valid message to check, was not able to fetch the message, or the message was not sent by you.";

            var (messageId, channelId) = ctx.MatchMessage(false);
            if (messageId == null || channelId == null)
                throw new PKError(failedToGetMessage);

            await using var conn = await _db.Obtain();

            var proxiedMsg = await _repo.GetMessage(conn, messageId.Value);
            if (proxiedMsg != null)
            {
                await ctx.Reply($"{Emojis.Success} This message was proxied successfully.");
                return;
            }

            // get the message info
            var msg = ctx.Message;
            try 
            {
                msg = await _rest.GetMessage(channelId.Value, messageId.Value);
            }
            catch (ForbiddenException)
            {
                throw new PKError(failedToGetMessage);
            }

            // if user is fetching a message in a different channel sent by someone else, throw a generic error message
            if (msg == null || (msg.Author.Id != ctx.Author.Id && msg.ChannelId != ctx.Channel.Id))
                throw new PKError(failedToGetMessage);

            if ((_botConfig.Prefixes ?? BotConfig.DefaultPrefixes).Any(p => msg.Content.StartsWith(p)))
                throw new PKError("This message starts with the bot's prefix, and was parsed as a command.");
            if (msg.WebhookId != null)
                throw new PKError("You cannot check messages sent by a webhook.");
            if (msg.Author.Id != ctx.Author.Id)
                throw new PKError("You can only check your own messages.");

            // get the channel info
            var channel = _cache.GetChannel(channelId.Value);
            if (channel == null)
                throw new PKError("Unable to get the channel associated with this message.");

            // using channel.GuildId here since _rest.GetMessage() doesn't return the GuildId
            var context = await _repo.GetMessageContext(conn, msg.Author.Id, channel.GuildId.Value, msg.ChannelId);
            var members = (await _repo.GetProxyMembers(conn, msg.Author.Id, channel.GuildId.Value)).ToList();

            // Run everything through the checks, catch the ProxyCheckFailedException, and reply with the error message.
            try 
            { 
                _proxy.ShouldProxy(channel, msg, context);
                _matcher.TryMatch(context, members, out var match, msg.Content, msg.Attachments.Length > 0, context.AllowAutoproxy);

                await ctx.Reply("I'm not sure why this message was not proxied, sorry.");
            } catch (ProxyService.ProxyChecksFailedException e)
            {
                await ctx.Reply($"{e.Message}");
            }
        }
    }
}