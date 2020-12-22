using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using Serilog;

namespace Myriad.Rest
{
    public class DiscordApiClient
    {
        private const string UserAgent = "Test Discord Library by @Ske#6201";
        private readonly BaseRestClient _client;

        public DiscordApiClient(string token, ILogger logger)
        {
            _client = new BaseRestClient(UserAgent, token, logger);
        }

        public Task<GatewayInfo> GetGateway() =>
            _client.Get<GatewayInfo>("/gateway", ("GetGateway", default))!;

        public Task<GatewayInfo.Bot> GetGatewayBot() =>
            _client.Get<GatewayInfo.Bot>("/gateway/bot", ("GetGatewayBot", default))!;

        public Task<Channel?> GetChannel(ulong channelId) =>
            _client.Get<Channel>($"/channels/{channelId}", ("GetChannel", channelId));

        public Task<Message?> GetMessage(ulong channelId, ulong messageId) =>
            _client.Get<Message>($"/channels/{channelId}/messages/{messageId}", ("GetMessage", channelId));

        public Task<Channel?> GetGuild(ulong id) =>
            _client.Get<Channel>($"/guilds/{id}", ("GetGuild", id));

        public Task<User?> GetUser(ulong id) =>
            _client.Get<User>($"/users/{id}", ("GetUser", default));

        public Task<Message> CreateMessage(ulong channelId, MessageRequest request) =>
            _client.Post<Message>($"/channels/{channelId}/messages", ("CreateMessage", channelId), request)!;

        public Task<Message> EditMessage(ulong channelId, ulong messageId, MessageEditRequest request) =>
            _client.Patch<Message>($"/channels/{channelId}/messages/{messageId}", ("EditMessage", channelId), request)!;

        public Task DeleteMessage(ulong channelId, ulong messageId) =>
            _client.Delete($"/channels/{channelId}/messages/{messageId}", ("DeleteMessage", channelId));

        public Task CreateReaction(ulong channelId, ulong messageId, Emoji emoji) =>
            _client.Put<object>($"/channels/{channelId}/messages/{messageId}/reactions/{EncodeEmoji(emoji)}/@me",
                ("CreateReaction", channelId), null);

        public Task DeleteOwnReaction(ulong channelId, ulong messageId, Emoji emoji) =>
            _client.Delete($"/channels/{channelId}/messages/{messageId}/reactions/{EncodeEmoji(emoji)}/@me",
                ("DeleteOwnReaction", channelId));

        public Task DeleteUserReaction(ulong channelId, ulong messageId, Emoji emoji, ulong userId) =>
            _client.Delete($"/channels/{channelId}/messages/{messageId}/reactions/{EncodeEmoji(emoji)}/{userId}",
                ("DeleteUserReaction", channelId));

        public Task DeleteAllReactions(ulong channelId, ulong messageId) =>
            _client.Delete($"/channels/{channelId}/messages/{messageId}/reactions",
                ("DeleteAllReactions", channelId));

        public Task DeleteAllReactionsForEmoji(ulong channelId, ulong messageId, Emoji emoji) =>
            _client.Delete($"/channels/{channelId}/messages/{messageId}/reactions/{EncodeEmoji(emoji)}",
                ("DeleteAllReactionsForEmoji", channelId));

        public Task<ApplicationCommand> CreateGlobalApplicationCommand(ulong applicationId,
                                                                       ApplicationCommandRequest request) =>
            _client.Post<ApplicationCommand>($"/applications/{applicationId}/commands",
                ("CreateGlobalApplicationCommand", applicationId), request)!;

        public Task<ApplicationCommand[]> GetGuildApplicationCommands(ulong applicationId, ulong guildId) =>
            _client.Get<ApplicationCommand[]>($"/applications/{applicationId}/guilds/{guildId}/commands",
                ("GetGuildApplicationCommands", applicationId))!;

        public Task<ApplicationCommand> CreateGuildApplicationCommand(ulong applicationId, ulong guildId,
                                                                      ApplicationCommandRequest request) =>
            _client.Post<ApplicationCommand>($"/applications/{applicationId}/guilds/{guildId}/commands",
                ("CreateGuildApplicationCommand", applicationId), request)!;

        public Task<ApplicationCommand> EditGuildApplicationCommand(ulong applicationId, ulong guildId,
                                                                    ApplicationCommandRequest request) =>
            _client.Patch<ApplicationCommand>($"/applications/{applicationId}/guilds/{guildId}/commands",
                ("EditGuildApplicationCommand", applicationId), request)!;

        public Task DeleteGuildApplicationCommand(ulong applicationId, ulong commandId) =>
            _client.Delete($"/applications/{applicationId}/commands/{commandId}",
                ("DeleteGuildApplicationCommand", applicationId));

        public Task CreateInteractionResponse(ulong interactionId, string token, InteractionResponse response) =>
            _client.Post<object>($"/interactions/{interactionId}/{token}/callback",
                ("CreateInteractionResponse", interactionId), response);

        public Task ModifyGuildMember(ulong guildId, ulong userId, ModifyGuildMemberRequest request) =>
            _client.Patch<object>($"/guilds/{guildId}/members/{userId}",
                ("ModifyGuildMember", guildId), request);

        public Task<Webhook> CreateWebhook(ulong channelId, CreateWebhookRequest request) =>
            _client.Post<Webhook>($"/channels/{channelId}/webhooks", ("CreateWebhook", channelId), request)!;
        
        public Task<Webhook> GetWebhook(ulong webhookId) =>
            _client.Get<Webhook>($"/webhooks/{webhookId}/webhooks", ("GetWebhook", webhookId))!;

        public Task<Webhook[]> GetChannelWebhooks(ulong channelId) =>
            _client.Get<Webhook[]>($"/channels/{channelId}/webhooks", ("GetChannelWebhooks", channelId))!;

        public Task<Message> ExecuteWebhook(ulong webhookId, string webhookToken, ExecuteWebhookRequest request,
                                            MultipartFile[]? files = null) =>
            _client.PostMultipart<Message>($"/webhooks/{webhookId}/{webhookToken}",
                ("ExecuteWebhook", webhookId), request, files)!;

        private static string EncodeEmoji(Emoji emoji) =>
            WebUtility.UrlEncode(emoji.Name) ?? emoji.Id?.ToString() ??
            throw new ArgumentException("Could not encode emoji");
    }
}