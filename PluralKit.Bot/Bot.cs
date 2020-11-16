using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using App.Metrics;

using Autofac;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;

using NodaTime;

using PluralKit.Core;

using Sentry;

using Serilog;
using Serilog.Context;

namespace PluralKit.Bot
{
    public class Bot
    {
        private readonly DiscordShardedClient _client;
        private readonly ILogger _logger;
        private readonly ILifetimeScope _services;
        private readonly PeriodicStatCollector _collector;
        private readonly IMetrics _metrics;
        private readonly ErrorMessageService _errorMessageService;
        private readonly CommandMessageService _commandMessageService;

        private bool _hasReceivedReady = false;
        private Timer _periodicTask; // Never read, just kept here for GC reasons

        public Bot(DiscordShardedClient client, ILifetimeScope services, ILogger logger, PeriodicStatCollector collector, IMetrics metrics, 
            ErrorMessageService errorMessageService, CommandMessageService commandMessageService)
        {
            _client = client;
            _logger = logger.ForContext<Bot>();
            _services = services;
            _collector = collector;
            _metrics = metrics;
            _errorMessageService = errorMessageService;
            _commandMessageService = commandMessageService;
        }

        public void Init()
        {
            // HandleEvent takes a type parameter, automatically inferred by the event type
            // It will then look up an IEventHandler<TypeOfEvent> in the DI container and call that object's handler method
            // For registering new ones, see Modules.cs 
            _client.MessageCreated += HandleEvent;
            _client.MessageDeleted += HandleEvent;
            _client.MessageUpdated += HandleEvent;
            _client.MessagesBulkDeleted += HandleEvent;
            _client.MessageReactionAdded += HandleEvent;

            // Update shard status for shards immediately on connect
            _client.Ready += (client, _) =>
            {
                _hasReceivedReady = true;
                return UpdateBotStatus(client);
            }; 
            _client.Resumed += (client, _) => UpdateBotStatus(client); 
            
            // Init the shard stuff
            _services.Resolve<ShardInfoService>().Init();

            // Not awaited, just needs to run in the background
            // Trying our best to run it at whole minute boundaries (xx:00), with ~250ms buffer
            // This *probably* doesn't matter in practice but I jut think it's neat, y'know.
            var timeNow = SystemClock.Instance.GetCurrentInstant();
            var timeTillNextWholeMinute = TimeSpan.FromMilliseconds(60000 - timeNow.ToUnixTimeMilliseconds() % 60000 + 250);
            _periodicTask = new Timer(_ =>
            {
                 var __ = UpdatePeriodic();
            }, null, timeTillNextWholeMinute, TimeSpan.FromMinutes(1));
        }

        public async Task Shutdown()
        {
            // This will stop the timer and prevent any subsequent invocations
            await _periodicTask.DisposeAsync();

            // Send users a lil status message
            // We're not actually properly disconnecting from the gateway (lol)  so it'll linger for a few minutes
            // Should be plenty of time for the bot to connect again next startup and set the real status
            if (_hasReceivedReady)
                await _client.UpdateStatusAsync(new DiscordActivity("Restarting... (please wait)"), UserStatus.Idle);
        }

        private Task HandleEvent<T>(DiscordClient shard, T evt) where T: DiscordEventArgs
        {
            // We don't want to stall the event pipeline, so we'll "fork" inside here
            var _ = HandleEventInner();
            return Task.CompletedTask;

            async Task HandleEventInner()
            {
                using var _ = LogContext.PushProperty("EventId", Guid.NewGuid());
                _logger
                    .ForContext("Elastic", "yes?")
                    .Debug("Gateway event: {@Event}", evt);
                
                await using var serviceScope = _services.BeginLifetimeScope();
                
                // Also, find a Sentry enricher for the event type (if one is present), and ask it to put some event data in the Sentry scope
                var sentryEnricher = serviceScope.ResolveOptional<ISentryEnricher<T>>();
                sentryEnricher?.Enrich(serviceScope.Resolve<Scope>(), shard, evt);
                
                // Find an event handler that can handle the type of event (<T>) we're given
                var handler = serviceScope.Resolve<IEventHandler<T>>();
                var queue = serviceScope.ResolveOptional<HandlerQueue<T>>();

                try
                {
                    using var timer = _metrics.Measure.Timer.Time(BotMetrics.EventsHandled, 
                            new MetricTags("event", typeof(T).Name.Replace("EventArgs", "")));

                    // Delegate to the queue to see if it wants to handle this event
                    // the TryHandle call returns true if it's handled the event
                    // Usually it won't, so just pass it on to the main handler
                    if (queue == null || !await queue.TryHandle(evt)) 
                        await handler.Handle(shard, evt);
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
            _metrics.Measure.Meter.Mark(BotMetrics.BotErrors, exc.GetType().FullName);
            
            // Make this beforehand so we can access the event ID for logging
            var sentryEvent = new SentryEvent(exc);

            _logger
                .ForContext("Elastic", "yes?")
                .Error(exc, "Exception in event handler: {SentryEventId}", sentryEvent.EventId);

            // If the event is us responding to our own error messages, don't bother logging
            if (evt is MessageCreateEventArgs mc && mc.Author.Id == _client.CurrentUser.Id)
                return;

            var shouldReport = exc.IsOurProblem();
            if (shouldReport)
            {
                // Report error to Sentry
                // This will just no-op if there's no URL set
                var sentryScope = serviceScope.Resolve<Scope>();
                
                // Add some specific info about Discord error responses, as a breadcrumb
                if (exc is BadRequestException bre)
                    sentryScope.AddBreadcrumb(bre.WebResponse.Response, "response.error", data: new Dictionary<string, string>(bre.WebResponse.Headers)); 
                if (exc is NotFoundException nfe)
                    sentryScope.AddBreadcrumb(nfe.WebResponse.Response, "response.error", data: new Dictionary<string, string>(nfe.WebResponse.Headers)); 
                if (exc is UnauthorizedException ue)
                    sentryScope.AddBreadcrumb(ue.WebResponse.Response, "response.error", data: new Dictionary<string, string>(ue.WebResponse.Headers)); 
                
                SentrySdk.CaptureEvent(sentryEvent, sentryScope);

                // Once we've sent it to Sentry, report it to the user (if we have permission to)
                var reportChannel = handler.ErrorChannelFor(evt);
                if (reportChannel != null && reportChannel.BotHasAllPermissions(Permissions.SendMessages | Permissions.EmbedLinks))
                    await _errorMessageService.SendErrorMessage(reportChannel, sentryEvent.EventId.ToString());
            }
        }
        
        private async Task UpdatePeriodic()
        {
            _logger.Debug("Running once-per-minute scheduled tasks");

            await UpdateBotStatus();

            // Clean up message cache in postgres
            await _commandMessageService.CleanupOldMessages();

            // Collect some stats, submit them to the metrics backend
            await _collector.CollectStats();
            await Task.WhenAll(((IMetricsRoot) _metrics).ReportRunner.RunAllAsync());
            _logger.Debug("Submitted metrics to backend");
        }

        private async Task UpdateBotStatus(DiscordClient specificShard = null)
        {
            // If we're not on any shards, don't bother (this happens if the periodic timer fires before the first Ready)
            if (!_hasReceivedReady) return;
            
            var totalGuilds = _client.ShardClients.Values.Sum(c => c.Guilds.Count);
            try // DiscordClient may throw an exception if the socket is closed (e.g just after OP 7 received)
            {
                Task UpdateStatus(DiscordClient shard) =>
                    shard.UpdateStatusAsync(new DiscordActivity($"pk;help | in {totalGuilds} servers | shard #{shard.ShardId}")); 
                
                if (specificShard != null)
                    await UpdateStatus(specificShard);
                else // Run shard updates concurrently
                    await Task.WhenAll(_client.ShardClients.Values.Select(UpdateStatus));
            }
            catch (WebSocketException) { }
        }
    }
}