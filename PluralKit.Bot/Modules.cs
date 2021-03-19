using System;
using System.Net.Http;

using Autofac;

using Myriad.Cache;
using Myriad.Gateway;

using NodaTime;

using PluralKit.Core;

using Sentry;

using Serilog;

namespace PluralKit.Bot
{
    public class BotModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Clients
            builder.Register(c =>
            {
                var botConfig = c.Resolve<BotConfig>();
                return new GatewaySettings
                {
                    Token = botConfig.Token,
                    MaxShardConcurrency = botConfig.MaxShardConcurrency,
                    Intents = GatewayIntent.Guilds |
                              GatewayIntent.DirectMessages |
                              GatewayIntent.DirectMessageReactions |
                              GatewayIntent.GuildEmojis |
                              GatewayIntent.GuildMessages |
                              GatewayIntent.GuildWebhooks |
                              GatewayIntent.GuildMessageReactions
                };
            }).AsSelf().SingleInstance();
            builder.RegisterType<Cluster>().AsSelf().SingleInstance();
            builder.Register(c => new Myriad.Rest.DiscordApiClient(c.Resolve<BotConfig>().Token, c.Resolve<ILogger>()))
                .AsSelf().SingleInstance();
            builder.RegisterType<MemoryDiscordCache>().AsSelf().As<IDiscordCache>().SingleInstance();

            // Commands
            builder.RegisterType<CommandTree>().AsSelf();
            builder.RegisterType<Autoproxy>().AsSelf();
            builder.RegisterType<Fun>().AsSelf();
            builder.RegisterType<Groups>().AsSelf();
            builder.RegisterType<Help>().AsSelf();
            builder.RegisterType<ImportExport>().AsSelf();
            builder.RegisterType<Member>().AsSelf();
            builder.RegisterType<MemberAvatar>().AsSelf();
            builder.RegisterType<MemberEdit>().AsSelf();
            builder.RegisterType<MemberGroup>().AsSelf();
            builder.RegisterType<MemberProxy>().AsSelf();
            builder.RegisterType<Misc>().AsSelf();
            builder.RegisterType<Random>().AsSelf();
            builder.RegisterType<ServerConfig>().AsSelf();
            builder.RegisterType<Switch>().AsSelf();
            builder.RegisterType<System>().AsSelf();
            builder.RegisterType<SystemEdit>().AsSelf();
            builder.RegisterType<SystemFront>().AsSelf();
            builder.RegisterType<SystemLink>().AsSelf();
            builder.RegisterType<SystemList>().AsSelf();
            builder.RegisterType<Token>().AsSelf();
            
            // Bot core
            builder.RegisterType<Bot>().AsSelf().SingleInstance();
            builder.RegisterType<MessageCreated>().As<IEventHandler<MessageCreateEvent>>();
            builder.RegisterType<MessageDeleted>().As<IEventHandler<MessageDeleteEvent>>().As<IEventHandler<MessageDeleteBulkEvent>>();
            builder.RegisterType<MessageEdited>().As<IEventHandler<MessageUpdateEvent>>();
            builder.RegisterType<ReactionAdded>().As<IEventHandler<MessageReactionAddEvent>>();
            
            // Event handler queue
            builder.RegisterType<HandlerQueue<MessageCreateEvent>>().AsSelf().SingleInstance();
            builder.RegisterType<HandlerQueue<MessageReactionAddEvent>>().AsSelf().SingleInstance();
            
            // Bot services
            builder.RegisterType<EmbedService>().AsSelf().SingleInstance();
            builder.RegisterType<ProxyService>().AsSelf().SingleInstance();
            builder.RegisterType<LogChannelService>().AsSelf().SingleInstance();
            builder.RegisterType<DataFileService>().AsSelf().SingleInstance();
            builder.RegisterType<WebhookExecutorService>().AsSelf().SingleInstance();
            builder.RegisterType<WebhookCacheService>().AsSelf().SingleInstance();
            builder.RegisterType<ShardInfoService>().AsSelf().SingleInstance();
            builder.RegisterType<CpuStatService>().AsSelf().SingleInstance();
            builder.RegisterType<PeriodicStatCollector>().AsSelf().SingleInstance();
            builder.RegisterType<LastMessageCacheService>().AsSelf().SingleInstance();
            builder.RegisterType<LoggerCleanService>().AsSelf().SingleInstance();
            builder.RegisterType<ErrorMessageService>().AsSelf().SingleInstance();
            builder.RegisterType<CommandMessageService>().AsSelf().SingleInstance();
            builder.RegisterType<CommandReferenceStore>().AsSelf().SingleInstance();
            
            // Sentry stuff
            builder.Register(_ => new Scope(null)).AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<SentryEnricher>()
                .As<ISentryEnricher<MessageCreateEvent>>()
                .As<ISentryEnricher<MessageDeleteEvent>>()
                .As<ISentryEnricher<MessageUpdateEvent>>()
                .As<ISentryEnricher<MessageDeleteBulkEvent>>()
                .As<ISentryEnricher<MessageReactionAddEvent>>()
                .SingleInstance();
            
            // Proxy stuff
            builder.RegisterType<ProxyMatcher>().AsSelf().SingleInstance();
            builder.RegisterType<ProxyTagParser>().AsSelf().SingleInstance();

            // Utils
            builder.Register(c => new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            }).AsSelf().SingleInstance();
            builder.RegisterInstance(SystemClock.Instance).As<IClock>();

            builder.RegisterType<DiscordRequestObserver>().AsSelf().SingleInstance();
        }
    }
}