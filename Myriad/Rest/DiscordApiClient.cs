using System.Net;

using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using Serilog;

namespace Myriad.Rest;

public class DiscordApiClient
{
    public const string UserAgent = "DiscordBot (https://github.com/PluralKit/PluralKit/tree/main/Myriad/, v1)";
    private const string DefaultApiBaseUrl = "https://discord.com/api/v10";
    private readonly BaseRestClient _client;

    public EventHandler<(string, int, long)> OnResponseEvent;

    public DiscordApiClient(string token, ILogger logger, string? baseUrl = null)
    {
        _client = new BaseRestClient(UserAgent, token, logger, baseUrl ?? DefaultApiBaseUrl);
        _client.OnResponseEvent += (_, ev) => OnResponseEvent?.Invoke(null, ev);
    }

    public Task<GatewayInfo> GetGateway() =>
        _client.Get<GatewayInfo>("/gateway", ("GetGateway", default))!;

    public Task<GatewayInfo.Bot> GetGatewayBot() =>
        _client.Get<GatewayInfo.Bot>("/gateway/bot", ("GetGatewayBot", default))!;

    public Task<Channel?> GetChannel(ulong channelId) =>
        _client.Get<Channel>($"/channels/{channelId}", ("GetChannel", channelId));

    public Task<Message?> GetMessage(ulong channelId, ulong messageId) =>
        _client.Get<Message>($"/channels/{channelId}/messages/{messageId}", ("GetMessage", channelId));

    public Task<Guild?> GetGuild(ulong id) =>
        _client.Get<Guild>($"/guilds/{id}", ("GetGuild", id));

    public Task<Channel[]> GetGuildChannels(ulong id) =>
        _client.Get<Channel[]>($"/guilds/{id}/channels", ("GetGuildChannels", id))!;

    public Task<User?> GetUser(ulong id) =>
        _client.Get<User>($"/users/{id}", ("GetUser", default));

    public Task<GuildMember?> GetGuildMember(ulong guildId, ulong userId) =>
        _client.Get<GuildMember>($"/guilds/{guildId}/members/{userId}",
            ("GetGuildMember", guildId));

    public Task<Message[]> GetChannelMessages(ulong channelId, int? limit)
    {
        var url = $"/channels/{channelId}/messages";
        if (limit != null)
            url += $"?limit={limit}";

        return _client.Get<Message[]>(url, ("GetChannelMessages", channelId))!;
    }

    public Task<Message> CreateMessage(ulong channelId, MessageRequest request, MultipartFile[]? files = null) =>
        _client.PostMultipart<Message>($"/channels/{channelId}/messages", ("CreateMessage", channelId), request,
            files)!;

    public Task<Message> EditMessage(ulong channelId, ulong messageId, MessageEditRequest request) =>
        _client.Patch<Message>($"/channels/{channelId}/messages/{messageId}", ("EditMessage", channelId), request)!;

    public Task DeleteMessage(ulong channelId, ulong messageId) =>
        _client.Delete($"/channels/{channelId}/messages/{messageId}", ("DeleteMessage", channelId));

    public Task DeleteMessage(Message message) =>
        _client.Delete($"/channels/{message.ChannelId}/messages/{message.Id}",
            ("DeleteMessage", message.ChannelId));

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

    public Task<ApplicationCommand[]?> ReplaceGlobalApplicationCommands(ulong applicationId,
                                                                 List<ApplicationCommandRequest> requests) =>
        _client.Put<ApplicationCommand[]>($"/applications/{applicationId}/commands",
            ("ReplaceGlobalApplicationCommands", applicationId), requests);

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
                                        MultipartFile[]? files = null, ulong? threadId = null)
    {
        var url = $"/webhooks/{webhookId}/{webhookToken}?wait=true";
        if (threadId != null)
            url += $"&thread_id={threadId}";

        return _client.PostMultipart<Message>(url,
            ("ExecuteWebhook", webhookId), request, files)!;
    }

    public Task<Message> EditWebhookMessage(ulong webhookId, string webhookToken, ulong messageId,
                                            WebhookMessageEditRequest request, ulong? threadId = null)
    {
        var url = $"/webhooks/{webhookId}/{webhookToken}/messages/{messageId}";
        if (threadId != null)
            url += $"?thread_id={threadId}";

        return _client.Patch<Message>(url, ("EditWebhookMessage", webhookId), request)!;
    }

    public Task<Channel> CreateDm(ulong recipientId) =>
        _client.Post<Channel>("/users/@me/channels", ("CreateDM", default), new CreateDmRequest(recipientId))!;

    private static string EncodeEmoji(Emoji emoji) =>
        WebUtility.UrlEncode(emoji.Id != null ? $"{emoji.Name}:{emoji.Id}" : emoji.Name) ??
        throw new ArgumentException("Could not encode emoji");
}