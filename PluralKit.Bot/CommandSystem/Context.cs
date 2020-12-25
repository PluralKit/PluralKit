using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using App.Metrics;

using Autofac;

using DSharpPlus;
using DSharpPlus.Entities;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using PluralKit.Core;

using DiscordApiClient = Myriad.Rest.DiscordApiClient;

namespace PluralKit.Bot
{
    public class Context
    {
        private readonly ILifetimeScope _provider;

        private readonly DiscordRestClient _rest;
        private readonly DiscordApiClient _newRest;
        private readonly DiscordShardedClient _client;
        private readonly DiscordClient _shard = null;
        private readonly Shard _shardNew;
        private readonly Guild? _guild;
        private readonly Channel _channel;
        private readonly DiscordMessage _message = null;
        private readonly MessageCreateEvent _messageNew;
        private readonly Parameters _parameters;
        private readonly MessageContext _messageContext;
        private readonly PermissionSet _botPermissions;
        private readonly PermissionSet _userPermissions;

        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly PKSystem _senderSystem;
        private readonly IMetrics _metrics;
        private readonly CommandMessageService _commandMessageService;
        private readonly IDiscordCache _cache;

        private Command _currentCommand;

        public Context(ILifetimeScope provider, Shard shard, Guild? guild, Channel channel, MessageCreateEvent message, int commandParseOffset,
                       PKSystem senderSystem, MessageContext messageContext, PermissionSet botPermissions)
        {
            _rest = provider.Resolve<DiscordRestClient>();
            _client = provider.Resolve<DiscordShardedClient>();
            _messageNew = message;
            _shardNew = shard;
            _guild = guild;
            _channel = channel;
            _senderSystem = senderSystem;
            _messageContext = messageContext;
            _cache = provider.Resolve<IDiscordCache>();
            _db = provider.Resolve<IDatabase>();
            _repo = provider.Resolve<ModelRepository>();
            _metrics = provider.Resolve<IMetrics>();
            _provider = provider;
            _commandMessageService = provider.Resolve<CommandMessageService>();
            _parameters = new Parameters(message.Content.Substring(commandParseOffset));
            _newRest = provider.Resolve<DiscordApiClient>();

            _botPermissions = botPermissions;
            _userPermissions = _cache.PermissionsFor(message);
        }

        public IDiscordCache Cache => _cache;

        public DiscordUser Author => _message.Author;
        public DiscordChannel Channel => _message.Channel;
        public Channel ChannelNew => _channel;
        public User AuthorNew => _messageNew.Author;
        public GuildMemberPartial MemberNew => _messageNew.Member;
        public DiscordMessage Message => _message;
        public Message MessageNew => _messageNew;
        public DiscordGuild Guild => _message.Channel.Guild;
        public Guild GuildNew => _guild;
        public DiscordClient Shard => _shard;
        public DiscordShardedClient Client => _client;
        public MessageContext MessageContext => _messageContext;

        public PermissionSet BotPermissions => _botPermissions;
        public PermissionSet UserPermissions => _userPermissions;

        public DiscordRestClient Rest => _rest;
        public DiscordApiClient RestNew => _newRest;

        public PKSystem System => _senderSystem;
        
        public Parameters Parameters => _parameters;

        internal IDatabase Database => _db;
        internal ModelRepository Repository => _repo;

        public async Task<Message> Reply(string text = null, Embed embed = null, AllowedMentions? mentions = null)
        {
            if (!BotPermissions.HasFlag(PermissionSet.SendMessages))
                // Will be "swallowed" during the error handler anyway, this message is never shown.
                throw new PKError("PluralKit does not have permission to send messages in this channel.");

            if (embed != null && !BotPermissions.HasFlag(PermissionSet.EmbedLinks))
                throw new PKError("PluralKit does not have permission to send embeds in this channel. Please ensure I have the **Embed Links** permission enabled.");

            var msg = await _newRest.CreateMessage(_channel.Id, new MessageRequest
            {
                Content = text,
                Embed = embed,
                AllowedMentions = mentions
            });
            // TODO: mentions should default to empty and not null?

            if (embed != null)
            {
                // Sensitive information that might want to be deleted by :x: reaction is typically in an embed format (member cards, for example)
                // This may need to be changed at some point but works well enough for now
                await _commandMessageService.RegisterMessage(msg.Id, AuthorNew.Id);
            }

            return msg;
        }
        
        public async Task Execute<T>(Command commandDef, Func<T, Task> handler)
        {
            _currentCommand = commandDef;

            try
            {
                await handler(_provider.Resolve<T>());
                _metrics.Measure.Meter.Mark(BotMetrics.CommandsRun);
            }
            catch (PKSyntaxError e)
            {
                await Reply($"{Emojis.Error} {e.Message}\n**Command usage:**\n> pk;{commandDef.Usage}");
            }
            catch (PKError e)
            {
                await Reply($"{Emojis.Error} {e.Message}");
            }
            catch (TimeoutException)
            {
                // Got a complaint the old error was a bit too patronizing. Hopefully this is better?
                await Reply($"{Emojis.Error} Operation timed out, sorry. Try again, perhaps?");
            }
        }

        public LookupContext LookupContextFor(PKSystem target) => 
            System?.Id == target.Id ? LookupContext.ByOwner : LookupContext.ByNonOwner;
        
        public LookupContext LookupContextFor(SystemId systemId) => 
            System?.Id == systemId ? LookupContext.ByOwner : LookupContext.ByNonOwner;

        public LookupContext LookupContextFor(PKMember target) =>
            System?.Id == target.System ? LookupContext.ByOwner : LookupContext.ByNonOwner;
        
        public IComponentContext Services => _provider;
    }
}