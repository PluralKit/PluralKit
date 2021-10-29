#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Types;

using NodaTime;

using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class ProxiedMessage
    {
        private static readonly Duration EditTimeout = Duration.FromMinutes(10);

        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly EmbedService _embeds;
        private readonly IClock _clock;
        private readonly DiscordApiClient _rest;
        private readonly WebhookExecutorService _webhookExecutor;
        private readonly LogChannelService _logChannel;
        private readonly IDiscordCache _cache;

        public ProxiedMessage(IDatabase db, ModelRepository repo, EmbedService embeds, IClock clock, DiscordApiClient rest,
                              WebhookExecutorService webhookExecutor, LogChannelService logChannel, IDiscordCache cache)
        {
            _db = db;
            _repo = repo;
            _embeds = embeds;
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

            var newContent = ctx.RemainderOrNull().NormalizeLineEndSpacing();

            var originalMsg = await _rest.GetMessage(msg.Message.Channel, msg.Message.Mid);
            if (originalMsg == null)
                throw new PKError("Could not edit message.");

            try
            {
                var editedMsg = await _webhookExecutor.EditWebhookMessage(msg.Message.Channel, msg.Message.Mid, newContent);

                if (ctx.Guild == null)
                    await _rest.CreateReaction(ctx.Channel.Id, ctx.Message.Id, new() { Name = Emojis.Success });

                if (ctx.BotPermissions.HasFlag(PermissionSet.ManageMessages))
                    await _rest.DeleteMessage(ctx.Channel.Id, ctx.Message.Id);

                await _logChannel.LogMessage(ctx.MessageContext, msg.Message, ctx.Message, editedMsg, originalMsg!.Content!);
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

            var (referencedMessage, _) = ctx.MatchMessage(false);
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
                if (msg == null)
                    throw new PKError("Could not find a recent message to edit.");
            }

            return msg;
        }

        private async Task<PKMessage?> FindRecentMessage(Context ctx)
        {
            var lastMessage = await _repo.GetLastMessage(ctx.Guild.Id, ctx.Channel.Id, ctx.Author.Id);
            if (lastMessage == null)
                return null;

            var timestamp = DiscordUtils.SnowflakeToInstant(lastMessage.Mid);
            if (_clock.GetCurrentInstant() - timestamp > EditTimeout)
                return null;

            return lastMessage;
        }

        public async Task GetMessage(Context ctx)
        {
            var (messageId, _) = ctx.MatchMessage(true);
            if (messageId == null)
            {
                if (!ctx.HasNext())
                    throw new PKSyntaxError("You must pass a message ID or link.");
                throw new PKSyntaxError($"Could not parse {ctx.PeekArgument().AsCode()} as a message ID or link.");
            }

            var isDelete = ctx.Match("delete") || ctx.MatchFlag("delete");
            var isRaw = ctx.Match("raw") || ctx.MatchFlag("raw");

            var message = await _db.Execute(c => _repo.GetMessage(c, messageId.Value));
            if (message == null)
            {
                if (isDelete)
                {
                    await DeleteCommandMessage(ctx, messageId.Value);
                    return;
                }
                else
                    throw Errors.MessageNotFound(messageId.Value);
            }

            if (isDelete)
            {
                if (message.System.Id != ctx.System.Id)
                    throw new PKError("You can only delete your own messages.");

                await ctx.Rest.DeleteMessage(message.Message.Channel, message.Message.Mid);

                if (ctx.Guild != null)
                    await ctx.Rest.DeleteMessage(ctx.Message);
                else
                    await ctx.Rest.CreateReaction(ctx.Message.ChannelId, ctx.Message.Id, new() { Name = Emojis.Success });

                return;
            }
            if (isRaw)
            {
                //TODO: Try this in channel with no readMesssageHistory permissions
                var discordMessage = await _rest.GetMessage(message.Message.Channel, message.Message.Mid);
                if (discordMessage == null)
                    throw Errors.MessageNotFound(message.Message.Mid);

                var content = discordMessage.Content;
                if (content == null)
                    throw new PKError("No message content found in that message");

                await ctx.Reply(text: $"```{content}```");

                if (Regex.IsMatch(content, "```.*```", RegexOptions.Singleline))
                {
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                    await ctx.Rest.CreateMessage(
                        ctx.Channel.Id,
                        new MessageRequest { Content = "Message contains codeblocks, check the attachements" },
                        new[] {new MultipartFile("message.txt", stream) });
                }

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

        private async Task DeleteCommandMessage(Context ctx, ulong messageId)
        {
            var message = await _repo.GetCommandMessage(messageId);
            if (message == null)
                throw Errors.MessageNotFound(messageId);

            if (message.AuthorId != ctx.Author.Id)
                throw new PKError("You can only delete command messages queried by this account.");

            await ctx.Rest.DeleteMessage(message.ChannelId, message.MessageId);

            if (ctx.Guild != null)
                await ctx.Rest.DeleteMessage(ctx.Message);
            else
                await ctx.Rest.CreateReaction(ctx.Message.ChannelId, ctx.Message.Id, new() { Name = Emojis.Success });
        }
    }
}