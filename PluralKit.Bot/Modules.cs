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
            builder.RegisterType<SystemCommands>().AsSelf();
            builder.RegisterType<MemberCommands>().AsSelf();
            builder.RegisterType<SwitchCommands>().AsSelf();
            builder.RegisterType<LinkCommands>().AsSelf();
            builder.RegisterType<APICommands>().AsSelf();
            builder.RegisterType<ImportExportCommands>().AsSelf();
            builder.RegisterType<HelpCommands>().AsSelf();
            builder.RegisterType<ModCommands>().AsSelf();
            builder.RegisterType<MiscCommands>().AsSelf();
            builder.RegisterType<AutoproxyCommands>().AsSelf();
            
            // Bot core
            builder.RegisterType<Bot>().AsSelf().SingleInstance();
            builder.RegisterType<PKEventHandler>().AsSelf();
            
            // Bot services
            builder.RegisterType<EmbedService>().AsSelf().SingleInstance();
            builder.RegisterType<ProxyService>().AsSelf().SingleInstance();
            builder.RegisterType<LogChannelService>().AsSelf().SingleInstance();
            builder.RegisterType<DataFileService>().AsSelf().SingleInstance();
            builder.RegisterType<WebhookExecutorService>().AsSelf().SingleInstance();
            builder.RegisterType<ProxyCacheService>().AsSelf().SingleInstance();
            builder.RegisterType<WebhookCacheService>().AsSelf().SingleInstance();
            builder.RegisterType<AutoproxyCacheService>().AsSelf().SingleInstance();
            builder.RegisterType<ShardInfoService>().AsSelf().SingleInstance();
            builder.RegisterType<CpuStatService>().AsSelf().SingleInstance();
            builder.RegisterType<PeriodicStatCollector>().AsSelf().SingleInstance();
            
            // Sentry stuff
            builder.Register(_ => new Scope(null)).AsSelf().InstancePerLifetimeScope();
            
            // .NET stuff
            builder.Populate(new ServiceCollection()
                .AddMemoryCache());
            
            // Utils
            builder.Register(c => new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            }).AsSelf().SingleInstance();
        }
    }
}