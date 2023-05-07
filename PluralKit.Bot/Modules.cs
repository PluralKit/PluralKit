using App.Metrics;

using Autofac;

using Myriad.Cache;
using Myriad.Gateway;
using Myriad.Rest;

using NodaTime;

using PluralKit.Core;

using Sentry;

using Serilog;

using IClock = NodaTime.IClock;

namespace PluralKit.Bot;

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
                GatewayQueueUrl = botConfig.GatewayQueueUrl,
                UseRedisRatelimiter = botConfig.UseRedisRatelimiter,
                Intents = GatewayIntent.Guilds |
                          GatewayIntent.DirectMessages |
                          GatewayIntent.DirectMessageReactions |
                          GatewayIntent.GuildEmojis |
                          GatewayIntent.GuildMessages |
                          GatewayIntent.GuildWebhooks |
                          GatewayIntent.GuildMessageReactions |
                          GatewayIntent.MessageContent
            };
        }).AsSelf().SingleInstance();
        builder.RegisterType<Cluster>().AsSelf().SingleInstance();
        builder.RegisterType<RedisGatewayService>().AsSelf().SingleInstance();
        builder.Register<IDiscordCache>(c =>
        {
            var botConfig = c.Resolve<BotConfig>();

            if (botConfig.UseRedisCache)
                return new RedisDiscordCache(c.Resolve<ILogger>(), botConfig.ClientId);
            return new MemoryDiscordCache(botConfig.ClientId);
        }).AsSelf().SingleInstance();
        builder.RegisterType<PrivateChannelService>().AsSelf().SingleInstance();

        builder.Register(c =>
        {
            var client = new DiscordApiClient(
                c.Resolve<BotConfig>().Token,
                c.Resolve<ILogger>(),
                c.Resolve<BotConfig>().DiscordBaseUrl
            );

            var metrics = c.Resolve<IMetrics>();

            client.OnResponseEvent += (_, ev) =>
            {
                var (endpoint, statusCode, ticks) = ev;
                var timer = metrics.Provider.Timer.Instance(BotMetrics.DiscordApiRequests, new MetricTags(
                    new[] { "endpoint", "status_code" },
                    new[] { endpoint, statusCode.ToString() }
                ));
                timer.Record(ticks / 10, TimeUnit.Microseconds);
            };

            return client;
        }).AsSelf().SingleInstance();

        // Commands
        builder.RegisterType<CommandTree>().AsSelf();
        builder.RegisterType<Admin>().AsSelf();
        builder.RegisterType<Api>().AsSelf();
        builder.RegisterType<Autoproxy>().AsSelf();
        builder.RegisterType<Checks>().AsSelf();
        builder.RegisterType<Config>().AsSelf();
        builder.RegisterType<Fun>().AsSelf();
        builder.RegisterType<Groups>().AsSelf();
        builder.RegisterType<GroupMember>().AsSelf();
        builder.RegisterType<Help>().AsSelf();
        builder.RegisterType<ImportExport>().AsSelf();
        builder.RegisterType<Member>().AsSelf();
        builder.RegisterType<MemberAvatar>().AsSelf();
        builder.RegisterType<MemberEdit>().AsSelf();
        builder.RegisterType<MemberProxy>().AsSelf();
        builder.RegisterType<Misc>().AsSelf();
        builder.RegisterType<ProxiedMessage>().AsSelf();
        builder.RegisterType<Random>().AsSelf();
        builder.RegisterType<ServerConfig>().AsSelf();
        builder.RegisterType<Switch>().AsSelf();
        builder.RegisterType<System>().AsSelf();
        builder.RegisterType<SystemEdit>().AsSelf();
        builder.RegisterType<SystemFront>().AsSelf();
        builder.RegisterType<SystemLink>().AsSelf();
        builder.RegisterType<SystemList>().AsSelf();

        // Application commands
        builder.RegisterType<ApplicationCommandTree>().AsSelf();
        builder.RegisterType<ApplicationCommandProxiedMessage>().AsSelf();

        // Bot core
        builder.RegisterType<Bot>().AsSelf().SingleInstance();
        builder.RegisterType<MessageCreated>().As<IEventHandler<MessageCreateEvent>>();
        builder.RegisterType<MessageDeleted>().As<IEventHandler<MessageDeleteEvent>>()
            .As<IEventHandler<MessageDeleteBulkEvent>>();
        builder.RegisterType<MessageEdited>().As<IEventHandler<MessageUpdateEvent>>();
        builder.RegisterType<ReactionAdded>().As<IEventHandler<MessageReactionAddEvent>>();
        builder.RegisterType<InteractionCreated>().As<IEventHandler<InteractionCreateEvent>>();

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
        builder.RegisterType<InteractionDispatchService>().AsSelf().SingleInstance();

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
            Timeout = TimeSpan.FromSeconds(5),
            DefaultRequestHeaders = { { "User-Agent", DiscordApiClient.UserAgent } }
        }).AsSelf().SingleInstance();
        builder.RegisterInstance(SystemClock.Instance).As<IClock>();
        builder.RegisterType<SerilogGatewayEnricherFactory>().AsSelf().SingleInstance();
    }
}