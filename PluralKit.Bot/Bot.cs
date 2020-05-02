using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

using App.Metrics;

using Autofac;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using NodaTime;

using PluralKit.Core;

using Sentry;

using Serilog;
using Serilog.Events;

namespace PluralKit.Bot
{
    public class Bot
    {
        private readonly DiscordShardedClient _client;
        private readonly ILogger _logger;
        private readonly ILifetimeScope _services;
        private readonly PeriodicStatCollector _collector;
        private readonly IMetrics _metrics;

        private Task _periodicTask; // Never read, just kept here for GC reasons

        public Bot(DiscordShardedClient client, ILifetimeScope services, ILogger logger, PeriodicStatCollector collector, IMetrics metrics)
        {
            _client = client;
            _services = services;
            _collector = collector;
            _metrics = metrics;
            _logger = logger.ForContext<Bot>();
        }

        public void Init()
        {
            // Attach the handlers we need
            _client.DebugLogger.LogMessageReceived += FrameworkLog;
            
            // HandleEvent takes a type parameter, automatically inferred by the event type
            _client.MessageCreated += HandleEvent;
            _client.MessageDeleted += HandleEvent;
            _client.MessageUpdated += HandleEvent;
            _client.MessagesBulkDeleted += HandleEvent;
            _client.MessageReactionAdded += HandleEvent;
            
            // Init the shard stuff
            _services.Resolve<ShardInfoService>().Init(_client);

            // Not awaited, just needs to run in the background
            _periodicTask = UpdatePeriodic();
        }

        private Task HandleEvent<T>(T evt) where T: DiscordEventArgs
        {
            // We don't want to stall the event pipeline, so we'll "fork" inside here
            var _ = HandleEventInner();
            return Task.CompletedTask;

            async Task HandleEventInner()
            {
                var serviceScope = _services.BeginLifetimeScope();
                var handler = serviceScope.Resolve<IEventHandler<T>>();

                try
                {
                    await handler.Handle(evt);
                }
                catch (Exception exc)
                {
                    await HandleError(handler, evt, serviceScope, exc);
                }
            }
        }

        private async Task HandleError<T>(IEventHandler<T> handler, T evt, ILifetimeScope serviceScope, Exception exc)
            where T: DiscordEventArgs
        {
            _logger.Error(exc, "Exception in bot event handler");

            var shouldReport = exc.IsOurProblem();
            if (shouldReport)
            {
                // Report error to Sentry
                // This will just no-op if there's no URL set
                var sentryEvent = new SentryEvent();
                var sentryScope = serviceScope.Resolve<Scope>();
                SentrySdk.CaptureEvent(sentryEvent, sentryScope);

                // Once we've sent it to Sentry, report it to the user (if we have permission to)
                var reportChannel = handler.ErrorChannelFor(evt);
                if (reportChannel != null && reportChannel.BotHasAllPermissions(Permissions.SendMessages))
                {
                    var eid = sentryEvent.EventId;
                    await reportChannel.SendMessageAsync(
                        $"{Emojis.Error} Internal error occurred. Please join the support server (<https://discord.gg/PczBt78>), and send the developer this ID: `{eid}`\nBe sure to include a description of what you were doing to make the error occur.");
                }
            }
        }
        
        private async Task UpdatePeriodic()
        {
            while (true)
            {
                // Run at every whole minute (:00), mostly because I feel like it
                var timeNow = SystemClock.Instance.GetCurrentInstant();
                var timeTillNextWholeMinute = 60000 - (timeNow.ToUnixTimeMilliseconds() % 60000);
                await Task.Delay((int) timeTillNextWholeMinute);
                
                // Change bot status
                var totalGuilds = _client.ShardClients.Values.Sum(c => c.Guilds.Count);
                try // DiscordClient may throw an exception if the socket is closed (e.g just after OP 7 received)
                {
                    foreach (var c in _client.ShardClients.Values)
                        await c.UpdateStatusAsync(new DiscordActivity($"pk;help | in {totalGuilds} servers | shard #{c.ShardId}"));
                }
                catch (WebSocketException) { }

                // Collect some stats, submit them to the metrics backend
                await _collector.CollectStats();
                await Task.WhenAll(((IMetricsRoot) _metrics).ReportRunner.RunAllAsync());
                _logger.Information("Submitted metrics to backend");
            }
        }
        private void FrameworkLog(object sender, DebugLogMessageEventArgs args)
        {
            // Bridge D#+ logging to Serilog
            LogEventLevel level = LogEventLevel.Verbose;
            if (args.Level == LogLevel.Critical)
                level = LogEventLevel.Fatal;
            else if (args.Level == LogLevel.Debug)
                level = LogEventLevel.Debug;
            else if (args.Level == LogLevel.Error)
                level = LogEventLevel.Error;
            else if (args.Level == LogLevel.Info)
                level = LogEventLevel.Information;
            else if (args.Level == LogLevel.Warning)
                level = LogEventLevel.Warning;

            _logger.Write(level, args.Exception, "D#+ {Source}: {Message}", args.Application, args.Message);
        }
    }
}