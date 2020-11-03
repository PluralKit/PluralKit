using System;
using System.Net.Http;

using Autofac;

using DSharpPlus;
using DSharpPlus.EventArgs;

using NodaTime;

using PluralKit.Core;

using Sentry;

namespace PluralKit.Bot
{
    public class BotModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Clients
            builder.Register(c => new DiscordConfiguration
            {
                Token = c.Resolve<BotConfig>().Token,
                TokenType = TokenType.Bot,
                MessageCacheSize = 0,
                LargeThreshold = 50
            }).AsSelf();
            builder.Register(c => new DiscordShardedClient(c.Resolve<DiscordConfiguration>())).AsSelf().SingleInstance();
            builder.Register(c => new DiscordRestClient(c.Resolve<DiscordConfiguration>())).AsSelf().SingleInstance();

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
            builder.RegisterType<MemberProxy>().AsSelf();
            builder.RegisterType<Misc>().AsSelf();
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
            builder.RegisterType<MessageCreated>().As<IEventHandler<MessageCreateEventArgs>>();
            builder.RegisterType<MessageDeleted>().As<IEventHandler<MessageDeleteEventArgs>>().As<IEventHandler<MessageBulkDeleteEventArgs>>();
            builder.RegisterType<MessageEdited>().As<IEventHandler<MessageUpdateEventArgs>>();
            builder.RegisterType<ReactionAdded>().As<IEventHandler<MessageReactionAddEventArgs>>();
            
            // Event handler queue
            builder.RegisterType<HandlerQueue<MessageCreateEventArgs>>().AsSelf().SingleInstance();
            builder.RegisterType<HandlerQueue<MessageReactionAddEventArgs>>().AsSelf().SingleInstance();
            
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
            
            // Sentry stuff
            builder.Register(_ => new Scope(null)).AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<SentryEnricher>()
                .As<ISentryEnricher<MessageCreateEventArgs>>()
                .As<ISentryEnricher<MessageDeleteEventArgs>>()
                .As<ISentryEnricher<MessageUpdateEventArgs>>()
                .As<ISentryEnricher<MessageBulkDeleteEventArgs>>()
                .As<ISentryEnricher<MessageReactionAddEventArgs>>()
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