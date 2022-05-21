using App.Metrics;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Types;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot;

public class MessageEdited: IEventHandler<MessageUpdateEvent>
{
    private readonly Bot _bot;
    private readonly IDiscordCache _cache;
    private readonly Cluster _client;
    private readonly IDatabase _db;
    private readonly LastMessageCacheService _lastMessageCache;
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;
    private readonly ProxyService _proxy;
    private readonly ModelRepository _repo;
    private readonly DiscordApiClient _rest;

    public MessageEdited(LastMessageCacheService lastMessageCache, ProxyService proxy, IDatabase db,
                         IMetrics metrics, ModelRepository repo, Cluster client, IDiscordCache cache, Bot bot,
                         DiscordApiClient rest, ILogger logger)
    {
        _lastMessageCache = lastMessageCache;
        _proxy = proxy;
        _db = db;
        _metrics = metrics;
        _repo = repo;
        _client = client;
        _cache = cache;
        _bot = bot;
        _rest = rest;
        _logger = logger.ForContext<MessageEdited>();
    }

    public async Task Handle(int shardId, MessageUpdateEvent evt)
    {
        if (evt.Author.Value?.Id == await _cache.GetOwnUser()) return;

        // Edit message events sometimes arrive with missing data; double-check it's all there
        if (!evt.Content.HasValue || !evt.Author.HasValue || !evt.Member.HasValue)
            return;

        var channel = await _cache.GetChannel(evt.ChannelId);
        if (!DiscordUtils.IsValidGuildChannel(channel))
            return;
        var guild = await _cache.GetGuild(channel.GuildId!.Value);
        var lastMessage = _lastMessageCache.GetLastMessage(evt.ChannelId)?.Current;

        // Only react to the last message in the channel
        if (lastMessage?.Id != evt.Id)
            return;

        // Just run the normal message handling code, with a flag to disable autoproxying
        MessageContext ctx;
        using (_metrics.Measure.Timer.Time(BotMetrics.MessageContextQueryTime))
            ctx = await _repo.GetMessageContext(evt.Author.Value!.Id, channel.GuildId!.Value, evt.ChannelId);

        var equivalentEvt = await GetMessageCreateEvent(evt, lastMessage, channel);
        var botPermissions = await _cache.PermissionsIn(channel.Id);

        try
        {
            await _proxy.HandleIncomingMessage(equivalentEvt, ctx, allowAutoproxy: false, guild: guild,
                channel: channel, botPermissions: botPermissions);
        }
        // Catch any failed proxy checks so they get ignored in the global error handler
        catch (ProxyService.ProxyChecksFailedException) { }
    }

    private async Task<MessageCreateEvent> GetMessageCreateEvent(MessageUpdateEvent evt, CachedMessage lastMessage,
                                                                 Channel channel)
    {
        var referencedMessage = await GetReferencedMessage(evt.ChannelId, lastMessage.ReferencedMessage);

        var messageReference = lastMessage.ReferencedMessage != null
            ? new Message.Reference(channel.GuildId, evt.ChannelId, lastMessage.ReferencedMessage.Value)
            : null;

        var messageType = lastMessage.ReferencedMessage != null
            ? Message.MessageType.Reply
            : Message.MessageType.Default;

        // TODO: is this missing anything?
        var equivalentEvt = new MessageCreateEvent
        {
            Id = evt.Id,
            ChannelId = evt.ChannelId,
            GuildId = channel.GuildId,
            Author = evt.Author.Value,
            Member = evt.Member.Value,
            Content = evt.Content.Value,
            Attachments = evt.Attachments.Value ?? Array.Empty<Message.Attachment>(),
            MessageReference = messageReference,
            ReferencedMessage = referencedMessage,
            Type = messageType,
        };
        return equivalentEvt;
    }

    private async Task<Message?> GetReferencedMessage(ulong channelId, ulong? referencedMessageId)
    {
        if (referencedMessageId == null)
            return null;

        var botPermissions = await _cache.PermissionsIn(channelId);
        if (!botPermissions.HasFlag(PermissionSet.ReadMessageHistory))
        {
            _logger.Warning(
                "Tried to get referenced message in channel {ChannelId} to reply but bot does not have Read Message History",
                channelId
            );
            return null;
        }

        return await _rest.GetMessage(channelId, referencedMessageId.Value);
    }
}