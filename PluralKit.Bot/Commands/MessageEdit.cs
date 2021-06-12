#nullable enable
using System.Threading.Tasks;

using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class MessageEdit
    {
        private static readonly Duration EditTimeout = Duration.FromMinutes(10);
        
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly IClock _clock;
        private readonly DiscordApiClient _rest;
        private readonly WebhookExecutorService _webhookExecutor;
        private readonly LogChannelService _logChannel;

        public MessageEdit(IDatabase db, ModelRepository repo, IClock clock, DiscordApiClient rest, WebhookExecutorService webhookExecutor, LogChannelService logChannel)
        {
            _db = db;
            _repo = repo;
            _clock = clock;
            _rest = rest;
            _webhookExecutor = webhookExecutor;
            _logChannel = logChannel;
        }

        public async Task EditMessage(Context ctx)
        {
            var msg = await GetMessageToEdit(ctx);
            if (!ctx.HasNext())
                throw new PKSyntaxError("You need to include the message to edit in.");

            if (ctx.Author.Id != msg.Sender)
                throw new PKError("Can't edit a message sent from a different account.");
            
            var newContent = ctx.RemainderOrNull();

            var originalMsg = await _rest.GetMessage(msg.Channel, msg.Mid);

            try
            {
                await _webhookExecutor.EditWebhookMessage(msg.Channel, msg.Mid, newContent);
                
                if (ctx.BotPermissions.HasFlag(PermissionSet.ManageMessages))
                    await _rest.DeleteMessage(ctx.Channel.Id, ctx.Message.Id);

                await _logChannel.LogEditedMessage(ctx.MessageContext, msg, ctx.Message, originalMsg!, newContent);
            }
            catch (NotFoundException)
            {
                throw new PKError("Could not edit message.");
            }
        }
        
        private async Task<PKMessage> GetMessageToEdit(Context ctx)
        {
            var referencedMessage = ctx.MatchMessage(false);
            if (referencedMessage != null)
            {
                await using var conn = await _db.Obtain();
                var msg = await _repo.GetMessage(conn, referencedMessage.Value);
                if (msg == null)
                    throw new PKError("This is not a message proxied by PluralKit.");
                
                return msg.Message;
            }

            if (ctx.Guild == null)
                throw new PKError("You must use a message link to edit messages in DMs.");

            var recent = await FindRecentMessage(ctx);
            if (recent == null)
                throw new PKError("Could not find a recent message to edit.");
            
            return recent;
        }

        private async Task<PKMessage?> FindRecentMessage(Context ctx)
        {
            await using var conn = await _db.Obtain();
            var lastMessage = await _repo.GetLastMessage(conn, ctx.Guild.Id, ctx.Channel.Id, ctx.Author.Id);
            if (lastMessage == null)
                return null;
            
            var timestamp = DiscordUtils.SnowflakeToInstant(lastMessage.Mid);
            if (_clock.GetCurrentInstant() - timestamp > EditTimeout)
                return null;

            return lastMessage;
        }
    }
}