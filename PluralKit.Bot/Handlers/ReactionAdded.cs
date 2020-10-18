using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    public class ReactionAdded: IEventHandler<MessageReactionAddEventArgs>
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly EmbedService _embeds;
        private readonly ILogger _logger;

        public ReactionAdded(EmbedService embeds, ILogger logger, IDatabase db, ModelRepository repo)
        {
            _embeds = embeds;
            _db = db;
            _repo = repo;
            _logger = logger.ForContext<ReactionAdded>();
        }

        public async Task Handle(MessageReactionAddEventArgs evt)
        { 
            await TryHandleProxyMessageReactions(evt);
        }

        private async ValueTask TryHandleProxyMessageReactions(MessageReactionAddEventArgs evt)
        {
            // Only proxies in guild text channels
            if (evt.Channel == null || evt.Channel.Type != ChannelType.Text) return;

            // Sometimes we get events from users that aren't in the user cache
            // In that case we get a "broken" user object (where eg. calling IsBot throws an exception)
            // We just ignore all of those for now, should be quite rare...
            if (!evt.Client.TryGetCachedUser(evt.User.Id, out _)) return;
            
            // Ignore reactions from bots (we can't DM them anyway)
            if (evt.User.IsBot) return;

            Task<FullMessage> GetMessage() => 
                _db.Execute(c => _repo.GetMessage(c, evt.Message.Id));

            FullMessage msg;
            CommandMessage cmdmsg;
            switch (evt.Emoji.Name)
            {
                // Message deletion
                case "\u274C": // Red X
                    if ((msg = await GetMessage()) != null)
                        await HandleDeleteReaction(evt, msg);
                    else if ((cmdmsg = await _db.Execute(conn => _repo.GetCommandMessage(conn, evt.Message.Id))) != null)
                        await HandleCommandDeleteReaction(evt, cmdmsg);
                    break;                
                
                case "\u2753": // Red question mark
                case "\u2754": // White question mark
                    if ((msg = await GetMessage()) != null) 
                        await HandleQueryReaction(evt, msg);
                    break;
                
                case "\U0001F514": // Bell
                case "\U0001F6CE": // Bellhop bell
                case "\U0001F3D3": // Ping pong paddle (lol)
                case "\u23F0": // Alarm clock
                case "\u2757": // Exclamation mark
                    if ((msg = await GetMessage()) != null)
                        await HandlePingReaction(evt, msg);
                    break;
            }
        }

        private async ValueTask HandleDeleteReaction(MessageReactionAddEventArgs evt, FullMessage msg)
        {
            if (!evt.Channel.BotHasAllPermissions(Permissions.ManageMessages)) return;
            
            // Can only delete your own message
            if (msg.Message.Sender != evt.User.Id) return;

            try
            {
                await evt.Message.DeleteAsync();
            }
            catch (NotFoundException)
            {
                // Message was deleted by something/someone else before we got to it
            }

            await _db.Execute(c => _repo.DeleteMessage(c, evt.Message.Id));
        }

        private async ValueTask HandleCommandDeleteReaction(MessageReactionAddEventArgs evt, CommandMessage msg)
        {
            if (!evt.Channel.BotHasAllPermissions(Permissions.ManageMessages)) return;

            // Can only delete your own message
            if (msg.author_id != evt.User.Id) return;

            try
            {
                await evt.Message.DeleteAsync();
            }
            catch (NotFoundException)
            {
                // Message was deleted by something/someone else before we got to it
            }

            // No need to delete database row here, it'll get deleted by the once-per-minute scheduled task.
        }

        private async ValueTask HandleQueryReaction(MessageReactionAddEventArgs evt, FullMessage msg)
        {
            // Try to DM the user info about the message
            var member = await evt.Guild.GetMember(evt.User.Id);
            try
            {
                await member.SendMessageAsync(embed: await _embeds.CreateMemberEmbed(msg.System, msg.Member, evt.Guild, LookupContext.ByNonOwner));
                await member.SendMessageAsync(embed: await _embeds.CreateMessageInfoEmbed(evt.Client, msg));
            }
            catch (UnauthorizedException) { } // No permissions to DM, can't check for this :(
            
            await TryRemoveOriginalReaction(evt);
        }

        private async ValueTask HandlePingReaction(MessageReactionAddEventArgs evt, FullMessage msg)
        {
            if (!evt.Channel.BotHasAllPermissions(Permissions.SendMessages)) return;
            
            // Check if the "pinger" has permission to send messages in this channel
            // (if not, PK shouldn't send messages on their behalf)
            var guildUser = await evt.Guild.GetMember(evt.User.Id);
            var requiredPerms = Permissions.AccessChannels | Permissions.SendMessages;
            if (guildUser == null || (guildUser.PermissionsIn(evt.Channel) & requiredPerms) != requiredPerms) return;
            
            if (msg.System.PingsEnabled)
            {
                // If the system has pings enabled, go ahead
                var embed = new DiscordEmbedBuilder().WithDescription($"[Jump to pinged message]({evt.Message.JumpLink})");
                await evt.Channel.SendMessageFixedAsync($"Psst, **{msg.Member.DisplayName()}** (<@{msg.Message.Sender}>), you have been pinged by <@{evt.User.Id}>.", embed: embed.Build(),
                    new IMention[] {new UserMention(msg.Message.Sender) });
            }
            else
            {
                // If not, tell them in DMs (if we can)
                try
                {
                    await guildUser.SendMessageFixedAsync($"{Emojis.Error} {msg.Member.DisplayName()}'s system has disabled reaction pings. If you want to mention them anyway, you can copy/paste the following message:");
                    await guildUser.SendMessageFixedAsync($"<@{msg.Message.Sender}>".AsCode());
                }
                catch (UnauthorizedException) { }
            }

            await TryRemoveOriginalReaction(evt);
        }

        private async Task TryRemoveOriginalReaction(MessageReactionAddEventArgs evt)
        {
            try
            {
                if (evt.Channel.BotHasAllPermissions(Permissions.ManageMessages))
                    await evt.Message.DeleteReactionAsync(evt.Emoji, evt.User);
            }
            catch (UnauthorizedException)
            {
                var botPerms = evt.Channel.BotPermissions();
                // So, in some cases (see Sentry issue 11K) the above check somehow doesn't work, and
                // Discord returns a 403 Unauthorized. TODO: figure out the root cause here instead of a workaround
                _logger.Warning("Attempted to remove reaction {Emoji} from user {User} on message {Channel}/{Message}, but got 403. Bot has permissions {Permissions} according to itself.",
                    evt.Emoji.Id, evt.User.Id, evt.Channel.Id, evt.Message.Id, botPerms);
            }
        }
    }
}