using System.Threading.Tasks;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    public class ReactionAdded: IEventHandler<MessageReactionAddEvent>
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly CommandMessageService _commandMessageService;
        private readonly ILogger _logger;
        private readonly IDiscordCache _cache;
        private readonly EmbedService _embeds;
        private readonly Bot _bot;
        private readonly DiscordApiClient _rest;

        public ReactionAdded(ILogger logger, IDatabase db, ModelRepository repo, CommandMessageService commandMessageService, IDiscordCache cache, Bot bot, DiscordApiClient rest, EmbedService embeds)
        {
            _db = db;
            _repo = repo;
            _commandMessageService = commandMessageService;
            _cache = cache;
            _bot = bot;
            _rest = rest;
            _embeds = embeds;
            _logger = logger.ForContext<ReactionAdded>();
        }

        public async Task Handle(Shard shard, MessageReactionAddEvent evt)
        { 
            await TryHandleProxyMessageReactions(evt);
        }

        private async ValueTask TryHandleProxyMessageReactions(MessageReactionAddEvent evt)
        {
            // Sometimes we get events from users that aren't in the user cache
            // We just ignore all of those for now, should be quite rare...
            if (!_cache.TryGetUser(evt.UserId, out var user))
                return;

            var channel = _cache.GetChannel(evt.ChannelId);

            // check if it's a command message first
            // since this can happen in DMs as well
            if (evt.Emoji.Name == "\u274c")
            {
                await using var conn = await _db.Obtain();
                var commandMsg = await _commandMessageService.GetCommandMessage(conn, evt.MessageId);
                if (commandMsg != null)
                {
                    await HandleCommandDeleteReaction(evt, commandMsg);
                    return;
                }
            }

            // Only proxies in guild text channels
            if (!DiscordUtils.IsValidGuildChannel(channel)) return;

            // Ignore reactions from bots (we can't DM them anyway)
            if (user.Bot) return;
            
            switch (evt.Emoji.Name)
            {
                // Message deletion
                case "\u274C": // Red X
                {
                    await using var conn = await _db.Obtain();
                    var msg = await _repo.GetMessage(conn, evt.MessageId);
                    if (msg != null)
                        await HandleProxyDeleteReaction(evt, msg);
                    
                    break;
                }
                case "\u2753": // Red question mark
                case "\u2754": // White question mark
                {
                    await using var conn = await _db.Obtain();
                    var msg = await _repo.GetMessage(conn, evt.MessageId);
                    if (msg != null)
                        await HandleQueryReaction(evt, msg);
                    
                    break;
                }

                case "\U0001F514": // Bell
                case "\U0001F6CE": // Bellhop bell
                case "\U0001F3D3": // Ping pong paddle (lol)
                case "\u23F0": // Alarm clock
                case "\u2757": // Exclamation mark
                {
                    await using var conn = await _db.Obtain();
                    var msg = await _repo.GetMessage(conn, evt.MessageId);
                    if (msg != null)
                        await HandlePingReaction(evt, msg);
                    break;
                }
            }
        }

        private async ValueTask HandleProxyDeleteReaction(MessageReactionAddEvent evt, FullMessage msg)
        {
            if (!_bot.PermissionsIn(evt.ChannelId).HasFlag(PermissionSet.ManageMessages))
                return;
            
            // Can only delete your own message
            if (msg.Message.Sender != evt.UserId) return;

            try
            {
                await _rest.DeleteMessage(evt.ChannelId, evt.MessageId);
            }
            catch (NotFoundException)
            {
                // Message was deleted by something/someone else before we got to it
            }

            await _db.Execute(c => _repo.DeleteMessage(c, evt.MessageId));
        }

        private async ValueTask HandleCommandDeleteReaction(MessageReactionAddEvent evt, CommandMessage msg)
        {
            // TODO: why does the bot need manage messages if it's deleting its own messages??
            if (!_bot.PermissionsIn(evt.ChannelId).HasFlag(PermissionSet.ManageMessages))
                return;

            // Can only delete your own message
            if (msg.AuthorId != evt.UserId) 
                return;

            try
            {
                await _rest.DeleteMessage(evt.ChannelId, evt.MessageId);
            }
            catch (NotFoundException)
            {
                // Message was deleted by something/someone else before we got to it
            }

            // No need to delete database row here, it'll get deleted by the once-per-minute scheduled task.
        }

        private async ValueTask HandleQueryReaction(MessageReactionAddEvent evt, FullMessage msg)
        {
            var guild = _cache.GetGuild(evt.GuildId!.Value);
            
            // Try to DM the user info about the message
            try
            {
                var dm = await _cache.GetOrCreateDmChannel(_rest, evt.UserId);
                await _rest.CreateMessage(dm.Id, new MessageRequest
                {
                    Embed = await _embeds.CreateMemberEmbed(msg.System, msg.Member, guild, LookupContext.ByNonOwner)
                });
                
                await _rest.CreateMessage(dm.Id, new MessageRequest
                {
                    Embed = await _embeds.CreateMessageInfoEmbed(msg)
                });
            }
            catch (ForbiddenException) { } // No permissions to DM, can't check for this :(
            
            await TryRemoveOriginalReaction(evt);
        }

        private async ValueTask HandlePingReaction(MessageReactionAddEvent evt, FullMessage msg)
        {
            if (!_bot.PermissionsIn(evt.ChannelId).HasFlag(PermissionSet.ManageMessages))
                return;
            
            // Check if the "pinger" has permission to send messages in this channel
            // (if not, PK shouldn't send messages on their behalf)
            var member = await _rest.GetGuildMember(evt.GuildId!.Value, evt.UserId);
            var requiredPerms = PermissionSet.ViewChannel | PermissionSet.SendMessages;
            if (member == null || !_cache.PermissionsFor(evt.ChannelId, member).HasFlag(requiredPerms)) return;
            
            if (msg.System.PingsEnabled)
            {
                // If the system has pings enabled, go ahead
                var embed = new EmbedBuilder().Description($"[Jump to pinged message]({evt.JumpLink()})");
                await _rest.CreateMessage(evt.ChannelId, new()
                {
                    Content =
                        $"Psst, **{msg.Member.DisplayName()}** (<@{msg.Message.Sender}>), you have been pinged by <@{evt.UserId}>.",
                    Embed = embed.Build(),
                    AllowedMentions = new AllowedMentions {Users = new[] {msg.Message.Sender}}
                });
            }
            else
            {
                // If not, tell them in DMs (if we can)
                try
                {
                    var dm = await _cache.GetOrCreateDmChannel(_rest, evt.UserId);
                    await _rest.CreateMessage(dm.Id, new MessageRequest
                    {
                        Content = $"{Emojis.Error} {msg.Member.DisplayName()}'s system has disabled reaction pings. If you want to mention them anyway, you can copy/paste the following message:"
                    });
                    await _rest.CreateMessage(dm.Id, new MessageRequest {Content = $"<@{msg.Message.Sender}>".AsCode()});
                }
                catch (ForbiddenException) { }
            }

            await TryRemoveOriginalReaction(evt);
        }

        private async Task TryRemoveOriginalReaction(MessageReactionAddEvent evt)
        {
            if (_bot.PermissionsIn(evt.ChannelId).HasFlag(PermissionSet.ManageMessages))
                await _rest.DeleteUserReaction(evt.ChannelId, evt.MessageId, evt.Emoji, evt.UserId);
        }
    }
}