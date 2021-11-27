using System.Net.WebSockets;

using App.Metrics;

using Autofac;

using Myriad.Cache;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

using Sentry;

using Serilog;
using Serilog.Context;

namespace PluralKit.Bot;

public class Bot
{
    private readonly IDiscordCache _cache;

    private readonly Cluster _cluster;
    private readonly PeriodicStatCollector _collector;
    private readonly CommandMessageService _commandMessageService;
    private readonly BotConfig _config;
    private readonly ErrorMessageService _errorMessageService;
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;
    private readonly DiscordApiClient _rest;
    private readonly ILifetimeScope _services;

    private bool _hasReceivedReady;
    private Timer _periodicTask; // Never read, just kept here for GC reasons

    public Bot(ILifetimeScope services, ILogger logger, PeriodicStatCollector collector, IMetrics metrics,
               BotConfig config,
               ErrorMessageService errorMessageService, CommandMessageService commandMessageService,
               Cluster cluster, DiscordApiClient rest, IDiscordCache cache)
    {
        _logger = logger.ForContext<Bot>();
        _services = services;
        _collector = collector;
        _metrics = metrics;
        _config = config;
        _errorMessageService = errorMessageService;
        _commandMessageService = commandMessageService;
        _cluster = cluster;
        _rest = rest;
        _cache = cache;
    }

    public void Init()
    {
        _cluster.EventReceived += OnEventReceived;

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

    private async Task OnEventReceived(Shard shard, IGatewayEvent evt)
    {
        await _cache.TryUpdateSelfMember(shard, evt);
        await _cache.HandleGatewayEvent(evt);

        // HandleEvent takes a type parameter, automatically inferred by the event type
        // It will then look up an IEventHandler<TypeOfEvent> in the DI container and call that object's handler method
        // For registering new ones, see Modules.cs
        if (evt is MessageCreateEvent mc)
            await HandleEvent(shard, mc);
        if (evt is MessageUpdateEvent mu)
            await HandleEvent(shard, mu);
        if (evt is MessageDeleteEvent md)
            await HandleEvent(shard, md);
        if (evt is MessageDeleteBulkEvent mdb)
            await HandleEvent(shard, mdb);
        if (evt is MessageReactionAddEvent mra)
            await HandleEvent(shard, mra);
        if (evt is InteractionCreateEvent ic)
            await HandleEvent(shard, ic);

        // Update shard status for shards immediately on connect
        if (evt is ReadyEvent re)
            await HandleReady(shard, re);
        if (evt is ResumedEvent)
            await HandleResumed(shard);
    }

    private Task HandleResumed(Shard shard) => UpdateBotStatus(shard);

    private Task HandleReady(Shard shard, ReadyEvent _)
    {
        _hasReceivedReady = true;
        return UpdateBotStatus(shard);
    }

    public async Task Shutdown()
    {
        // This will stop the timer and prevent any subsequent invocations
        await _periodicTask.DisposeAsync();

        // Send users a lil status message
        // We're not actually properly disconnecting from the gateway (lol)  so it'll linger for a few minutes
        // Should be plenty of time for the bot to connect again next startup and set the real status
        if (_hasReceivedReady)
            await Task.WhenAll(_cluster.Shards.Values.Select(shard =>
                shard.UpdateStatus(new GatewayStatusUpdate
                {
                    Activities = new[]
                    {
                        new Activity {Name = "Restarting... (please wait)", Type = ActivityType.Game}
                    },
                    Status = GatewayStatusUpdate.UserStatus.Idle
                })));
    }

    private Task HandleEvent<T>(Shard shard, T evt) where T : IGatewayEvent
    {
        // We don't want to stall the event pipeline, so we'll "fork" inside here
        var _ = HandleEventInner();
        return Task.CompletedTask;

        async Task HandleEventInner()
        {
            await Task.Yield();

            await using var serviceScope = _services.BeginLifetimeScope();

            // Find an event handler that can handle the type of event (<T>) we're given
            IEventHandler<T> handler;
            try
            {
                handler = serviceScope.Resolve<IEventHandler<T>>();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error instantiating handler class");
                return;
            }

            try
            {
                var queue = serviceScope.ResolveOptional<HandlerQueue<T>>();

                using var _ = LogContext.PushProperty("EventId", Guid.NewGuid());
                using var __ = LogContext.Push(await serviceScope.Resolve<SerilogGatewayEnricherFactory>()
                    .GetEnricher(shard, evt));
                _logger.Verbose("Received gateway event: {@Event}", evt);

                // Also, find a Sentry enricher for the event type (if one is present), and ask it to put some event data in the Sentry scope
                var sentryEnricher = serviceScope.ResolveOptional<ISentryEnricher<T>>();
                sentryEnricher?.Enrich(serviceScope.Resolve<Scope>(), shard, evt);

                using var timer = _metrics.Measure.Timer.Time(BotMetrics.EventsHandled,
                    new MetricTags("event", typeof(T).Name.Replace("Event", "")));

                // Delegate to the queue to see if it wants to handle this event
                // the TryHandle call returns true if it's handled the event
                // Usually it won't, so just pass it on to the main handler
                if (queue == null || !await queue.TryHandle(evt))
                    await handler.Handle(shard, evt);
            }
            catch (Exception exc)
            {
                await HandleError(shard, handler, evt, serviceScope, exc);
            }
        }
    }

    private async Task HandleError<T>(Shard shard, IEventHandler<T> handler, T evt, ILifetimeScope serviceScope,
                                      Exception exc)
        where T : IGatewayEvent
    {
        _metrics.Measure.Meter.Mark(BotMetrics.BotErrors, exc.GetType().FullName);

        // Make this beforehand so we can access the event ID for logging
        var sentryEvent = new SentryEvent(exc);

        // If the event is us responding to our own error messages, don't bother logging
        if (evt is MessageCreateEvent mc && mc.Author.Id == shard.User?.Id)
            return;

        var shouldReport = exc.IsOurProblem();
        if (shouldReport)
        {
            // only log exceptions if they're our problem
            _logger.Error(exc, "Exception in event handler: {SentryEventId}", sentryEvent.EventId);

            // Report error to Sentry
            // This will just no-op if there's no URL set
            var sentryScope = serviceScope.Resolve<Scope>();

            // Add some specific info about Discord error responses, as a breadcrumb
            // TODO: headers to dict
            // if (exc is BadRequestException bre)
            //     sentryScope.AddBreadcrumb(bre.Response, "response.error", data: new Dictionary<string, string>(bre.Response.Headers)); 
            // if (exc is NotFoundException nfe)
            //     sentryScope.AddBreadcrumb(nfe.Response, "response.error", data: new Dictionary<string, string>(nfe.Response.Headers)); 
            // if (exc is UnauthorizedException ue)
            //     sentryScope.AddBreadcrumb(ue.Response, "response.error", data: new Dictionary<string, string>(ue.Response.Headers)); 

            SentrySdk.CaptureEvent(sentryEvent, sentryScope);

            // most of these errors aren't useful...
            if (_config.DisableErrorReporting)
                return;

            // Once we've sent it to Sentry, report it to the user (if we have permission to)
            var reportChannel = handler.ErrorChannelFor(evt);
            if (reportChannel == null)
                return;

            var botPerms = await _cache.PermissionsIn(reportChannel.Value);
            if (botPerms.HasFlag(PermissionSet.SendMessages | PermissionSet.EmbedLinks))
                await _errorMessageService.SendErrorMessage(reportChannel.Value, sentryEvent.EventId.ToString());
        }
    }

    private async Task UpdatePeriodic()
    {
        _logger.Debug("Running once-per-minute scheduled tasks");

        await UpdateBotStatus();

        // Collect some stats, submit them to the metrics backend
        await _collector.CollectStats();
        await Task.WhenAll(((IMetricsRoot)_metrics).ReportRunner.RunAllAsync());
        _logger.Debug("Submitted metrics to backend");
    }

    private async Task UpdateBotStatus(Shard specificShard = null)
    {
        // If we're not on any shards, don't bother (this happens if the periodic timer fires before the first Ready)
        if (!_hasReceivedReady) return;

        var totalGuilds = await _cache.GetAllGuilds().CountAsync();

        try // DiscordClient may throw an exception if the socket is closed (e.g just after OP 7 received)
        {
            Task UpdateStatus(Shard shard) =>
                shard.UpdateStatus(new GatewayStatusUpdate
                {
                    Activities = new[]
                    {
                        new Activity
                        {
                            Name = $"{(_config.Prefixes ?? BotConfig.DefaultPrefixes)[0]}help | in {totalGuilds:N0} servers | shard #{shard.ShardId}",
                            Type = ActivityType.Game,
                            Url = "https://pluralkit.me/"
                        }
                    }
                });

            if (specificShard != null)
                await UpdateStatus(specificShard);
            else // Run shard updates concurrently
                await Task.WhenAll(_cluster.Shards.Values.Select(UpdateStatus));
        }
        catch (WebSocketException)
        {
            // TODO: this still thrown?
        }
    }
}