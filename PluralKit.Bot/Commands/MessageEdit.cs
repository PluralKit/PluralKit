﻿#nullable enable
using System.Threading.Tasks;

using Myriad.Cache;
using Myriad.Extensions;
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
        private readonly IDiscordCache _cache;

        public MessageEdit(IDatabase db, ModelRepository repo, IClock clock, DiscordApiClient rest, WebhookExecutorService webhookExecutor, LogChannelService logChannel, IDiscordCache cache)
        {
            _db = db;
            _repo = repo;
            _clock = clock;
            _rest = rest;
            _webhookExecutor = webhookExecutor;
            _logChannel = logChannel;
            _cache = cache;
        }

        public async Task EditMessage(Context ctx)
        {
            var msg = await GetMessageToEdit(ctx);
            if (!ctx.HasNext())
                throw new PKSyntaxError("You need to include the message to edit in.");

            if (ctx.System.Id != msg.System.Id)
                throw new PKError("Can't edit a message sent by a different system.");
            
            if (_cache.GetRootChannel(msg.Message.Channel).Id != msg.Message.Channel)
                throw new PKError("PluralKit cannot edit messages in threads.");

            var newContent = ctx.RemainderOrNull();

            var originalMsg = await _rest.GetMessage(msg.Message.Channel, msg.Message.Mid);

            try
            {
                await _webhookExecutor.EditWebhookMessage(msg.Message.Channel, msg.Message.Mid, newContent);
                
                if (ctx.Guild == null)
                    await _rest.CreateReaction(ctx.Channel.Id, ctx.Message.Id, new() { Name = Emojis.Success });

                if (ctx.BotPermissions.HasFlag(PermissionSet.ManageMessages))
                    await _rest.DeleteMessage(ctx.Channel.Id, ctx.Message.Id);

                await _logChannel.LogEditedMessage(ctx.MessageContext, msg, ctx.Message, originalMsg!, newContent);
            }
            catch (NotFoundException)
            {
                throw new PKError("Could not edit message.");
            }
        }
        
        private async Task<FullMessage> GetMessageToEdit(Context ctx)
        {
            await using var conn = await _db.Obtain();
            FullMessage? msg = null;

            var referencedMessage = ctx.MatchMessage(false);
            if (referencedMessage != null)
            {
                msg = await _repo.GetMessage(conn, referencedMessage.Value);
                if (msg == null)
                    throw new PKError("This is not a message proxied by PluralKit.");
            }

            if (msg == null)
            {
                if (ctx.Guild == null)
                    throw new PKError("You must use a message link to edit messages in DMs.");

                var recent = await FindRecentMessage(ctx);
                if (recent == null)
                    throw new PKError("Could not find a recent message to edit.");

                msg = await _repo.GetMessage(conn, recent.Mid);
            }

            return msg;
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
