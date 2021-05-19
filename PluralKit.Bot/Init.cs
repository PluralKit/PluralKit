using System;
using System.Threading;
using System.Threading.Tasks;

using Autofac;

using Microsoft.Extensions.Configuration;

using Myriad.Gateway;
using Myriad.Rest;

using PluralKit.Core;

using Serilog;
using Serilog.Core;

namespace PluralKit.Bot
{
    public class Init
    {
        static Task Main(string[] args)
        {
            // Load configuration and run global init stuff
            var config = InitUtils.BuildConfiguration(args).Build();
            InitUtils.InitStatic();
            
            // Set up DI container and modules
            var services = BuildContainer(config);
            
            return RunWrapper(services, async ct =>
            {
                var logger = services.Resolve<ILogger>().ForContext<Init>();
                
                // Initialize Sentry SDK, and make sure it gets dropped at the end
                using var _ = Sentry.SentrySdk.Init(services.Resolve<CoreConfig>().SentryUrl);

                // "Connect to the database" (ie. set off database migrations and ensure state)
                logger.Information("Connecting to database");
                await services.Resolve<IDatabase>().ApplyMigrations();
                
                // Init the bot instance itself, register handlers and such to the client before beginning to connect
                logger.Information("Initializing bot");
                var bot = services.Resolve<Bot>();
                await bot.Init();
                
                // Install observer for request/responses
                DiscordRequestObserver.Install(services);

                // Start the Discord shards themselves (handlers already set up)
                logger.Information("Connecting to Discord");
                var info = await services.Resolve<DiscordApiClient>().GetGatewayBot();
                await services.Resolve<Cluster>().Start(info);
                logger.Information("Connected! All is good (probably).");

                // Lastly, we just... wait. Everything else is handled in the DiscordClient event loop
                try
                {
                    await Task.Delay(-1, ct);
                }
                catch (TaskCanceledException)
                {
                    // Once the CancellationToken fires, we need to shut stuff down
                    // (generally happens given a SIGINT/SIGKILL/Ctrl-C, see calling wrapper)
                    await bot.Shutdown();
                }
            });
        }

        private static async Task RunWrapper(IContainer services, Func<CancellationToken, Task> taskFunc)
        {
            // This function does a couple things: 
            // - Creates a CancellationToken that'll cancel tasks once needed
            // - Wraps the given function in an exception handler that properly logs errors
            // - Adds a SIGINT (Ctrl-C) listener through Console.CancelKeyPress to gracefully shut down
            // - Adds a SIGTERM (kill, systemctl stop, docker stop) listener through AppDomain.ProcessExit (same as above)
            var logger = services.Resolve<ILogger>().ForContext<Init>();

            var shutdown = new TaskCompletionSource<object>();
            var gracefulShutdownCts = new CancellationTokenSource();
            
            Console.CancelKeyPress += delegate
            {
                // ReSharper disable once AccessToDisposedClosure (will only be hit before the below disposal)
                logger.Information("Received SIGINT/Ctrl-C, attempting graceful shutdown...");
                gracefulShutdownCts.Cancel();
            };
            
            AppDomain.CurrentDomain.ProcessExit += (_, __) =>
            {
                // This callback is fired on a SIGKILL is sent.
                // The runtime will kill the program as soon as this callback is finished, so we have to
                // block on the shutdown task's completion to ensure everything is sorted by the time this returns.

                // ReSharper disable once AccessToDisposedClosure (it's only disposed after the block)
                logger.Information("Received SIGKILL event, attempting graceful shutdown...");
                gracefulShutdownCts.Cancel();
                var ___ = shutdown.Task.Result; // Blocking! This is the only time it's justified...
            };

            try
            {
                await taskFunc(gracefulShutdownCts.Token);
                logger.Information("Shutdown complete. Have a nice day~");
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Error while running bot");
            }
            
            // Allow the log buffer to flush properly before exiting
            ((Logger) logger).Dispose();
            await Task.Delay(500);
            shutdown.SetResult(null);
        }

        private static IContainer BuildContainer(IConfiguration config)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(config);
            builder.RegisterModule(new ConfigModule<BotConfig>("Bot"));
            builder.RegisterModule(new LoggingModule("bot", cfg =>
            {
                // TODO: do we need this?
                // cfg.Destructure.With<EventDestructuring>();
            }));
            builder.RegisterModule(new MetricsModule());
            builder.RegisterModule<DataStoreModule>();
            builder.RegisterModule<BotModule>();
            return builder.Build();
        }
    }
}