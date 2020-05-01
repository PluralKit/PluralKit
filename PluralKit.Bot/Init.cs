using System;
using System.Threading;
using System.Threading.Tasks;

using Autofac;

using DSharpPlus;

using Microsoft.Extensions.Configuration;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    public class Init
    {
        static Task Main(string[] args)
        {
            // Load configuration and run global init stuff
            var config = InitUtils.BuildConfiguration(args).Build();
            InitUtils.Init();
            
            // Set up DI container and modules
            var services = BuildContainer(config);
            
            return RunWrapper(services, async ct =>
            {
                var logger = services.Resolve<ILogger>().ForContext<Init>();
                
                // Initialize Sentry SDK, and make sure it gets dropped at the end
                using var _ = Sentry.SentrySdk.Init(services.Resolve<CoreConfig>().SentryUrl);

                // "Connect to the database" (ie. set off database migrations and ensure state)
                logger.Information("Connecting to database");
                await services.Resolve<SchemaService>().ApplyMigrations();
                
                // Start the Discord client; StartAsync returns once shard instances are *created* (not necessarily connected)
                logger.Information("Connecting to Discord");
                await services.Resolve<DiscordShardedClient>().StartAsync();
                
                // Start the bot stuff and let it register things
                services.Resolve<Bot>().Init();
                
                // Lastly, we just... wait. Everything else is handled in the DiscordClient event loop
                await Task.Delay(-1, ct);
            });
        }

        private static async Task RunWrapper(IContainer services, Func<CancellationToken, Task> taskFunc)
        {
            // This function does a couple things: 
            // - Creates a CancellationToken that'll cancel tasks once we get a Ctrl-C / SIGINT
            // - Wraps the given function in an exception handler that properly logs errors
            var logger = services.Resolve<ILogger>().ForContext<Init>();
            
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += delegate { cts.Cancel(); };

            try
            {
                await taskFunc(cts.Token);
            }
            catch (TaskCanceledException e) when (e.CancellationToken == cts.Token)
            {
                // The CancellationToken we made got triggered - this is normal!
                // Therefore, exception handler is empty.
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Error while running bot");
                
                // Allow the log buffer to flush properly before exiting
                await Task.Delay(1000, cts.Token);
            }
        }

        private static IContainer BuildContainer(IConfiguration config)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(config);
            builder.RegisterModule(new ConfigModule<BotConfig>("Bot"));
            builder.RegisterModule(new LoggingModule("bot"));
            builder.RegisterModule(new MetricsModule());
            builder.RegisterModule<DataStoreModule>();
            builder.RegisterModule<BotModule>();
            return builder.Build();
        }
    }
}