using System;
using System.Threading.Tasks;

using App.Metrics;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Types;

using PluralKit.Core;

using Serilog;


namespace PluralKit.Bot
{
    public class MessageEdited: IEventHandler<MessageUpdateEvent>
    {
        private readonly LastMessageCacheService _lastMessageCache;
        private readonly ProxyService _proxy;
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly IMetrics _metrics;
        private readonly Cluster _client;
        private readonly IDiscordCache _cache;
        private readonly Bot _bot;
        private readonly DiscordApiClient _rest;
        private readonly ILogger _logger;
        
        public MessageEdited(LastMessageCacheService lastMessageCache, ProxyService proxy, IDatabase db, IMetrics metrics, ModelRepository repo, Cluster client, IDiscordCache cache, Bot bot, DiscordApiClient rest, ILogger logger)
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

        public async Task Handle(Shard shard, MessageUpdateEvent evt)
        {
            if (evt.Author.Value?.Id == _client.User?.Id) return;
            
            // Edit message events sometimes arrive with missing data; double-check it's all there
            if (!evt.Content.HasValue || !evt.Author.HasValue || !evt.Member.HasValue) 
                return;
            
            var channel = _cache.GetChannel(evt.ChannelId);
            if (channel.Type != Channel.ChannelType.GuildText)
                return;
            var guild = _cache.GetGuild(channel.GuildId!.Value);
            var lastMessage = _lastMessageCache.GetLastMessage(evt.ChannelId);

            // Only react to the last message in the channel
            if (lastMessage?.Id != evt.Id)
                return;
            
            // Just run the normal message handling code, with a flag to disable autoproxying
            MessageContext ctx;
            await using (var conn = await _db.Obtain())
            using (_metrics.Measure.Timer.Time(BotMetrics.MessageContextQueryTime))
                ctx = await _repo.GetMessageContext(conn, evt.Author.Value!.Id, channel.GuildId!.Value, evt.ChannelId);

            var equivalentEvt = await GetMessageCreateEvent(evt, lastMessage, channel);
            var botPermissions = _bot.PermissionsIn(channel.Id);
            await _proxy.HandleIncomingMessage(shard, equivalentEvt, ctx, allowAutoproxy: false, guild: guild, channel: channel, botPermissions: botPermissions);
        }

        private async Task<MessageCreateEvent> GetMessageCreateEvent(MessageUpdateEvent evt, CachedMessage lastMessage, Channel channel)
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
            
            var botPermissions = _bot.PermissionsIn(channelId);
            if (!botPermissions.HasFlag(PermissionSet.ReadMessageHistory))
            {
                _logger.Warning("Tried to get referenced message in channel {ChannelId} to reply but bot does not have Read Message History",
                    channelId);
                return null;
            }

            return await _rest.GetMessage(channelId, referencedMessageId.Value);
        }
    }
}