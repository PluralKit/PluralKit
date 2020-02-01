using System;
using System.Net.Http;

using Autofac;
using Autofac.Extensions.DependencyInjection;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

using PluralKit.Bot.Commands;

using Sentry;

namespace PluralKit.Bot
{
    public class BotModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Client
            builder.Register(c => new DiscordShardedClient(new DiscordSocketConfig()
            {
                MessageCacheSize = 0,
                ConnectionTimeout = 2 * 60 * 1000,
                ExclusiveBulkDelete = true,
                LargeThreshold = 50,
                DefaultRetryMode = RetryMode.RetryTimeouts | RetryMode.RetryRatelimit
                // Commented this out since Debug actually sends, uh, quite a lot that's not necessary in production
                // but leaving it here in case I (or someone else) get[s] confused about why logging isn't working again :p
                // LogLevel = LogSeverity.Debug // We filter log levels in Serilog, so just pass everything through (Debug is lower than Verbose)
            })).AsSelf().As<BaseDiscordClient>().As<BaseSocketClient>().As<IDiscordClient>().SingleInstance();
            
            // Commands
            builder.RegisterType<CommandTree>().AsSelf();
            builder.RegisterType<Autoproxy>().AsSelf();
            builder.RegisterType<Fun>().AsSelf();
            builder.RegisterType<Help>().AsSelf();
            builder.RegisterType<ImportExport>().AsSelf();
            builder.RegisterType<Member>().AsSelf();
            builder.RegisterType<MemberAvatar>().AsSelf();
            builder.RegisterType<MemberEdit>().AsSelf();
            builder.RegisterType<MemberProxy>().AsSelf();
            builder.RegisterType<Misc>().AsSelf();
            builder.RegisterType<ServerConfig>().AsSelf();
            builder.RegisterType<Switch>().AsSelf();
            builder.RegisterType<Commands.System>().AsSelf();
            builder.RegisterType<SystemEdit>().AsSelf();
            builder.RegisterType<SystemFront>().AsSelf();
            builder.RegisterType<SystemLink>().AsSelf();
            builder.RegisterType<SystemList>().AsSelf();
            builder.RegisterType<Token>().AsSelf();
            
            // Bot core
            builder.RegisterType<Bot>().AsSelf().SingleInstance();
            builder.RegisterType<PKEventHandler>().AsSelf();
            
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
            
            // Sentry stuff
            builder.Register(_ => new Scope(null)).AsSelf().InstancePerLifetimeScope();
            
            
            // Utils
            builder.Register(c => new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            }).AsSelf().SingleInstance();
        }
    }
}