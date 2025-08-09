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
    private readonly RedisService _redis;
    private readonly ILifetimeScope _services;
    private readonly RuntimeConfigService _runtimeConfig;

    private Timer _periodicTask; // Never read, just kept here for GC reasons

    public Bot(ILifetimeScope services, ILogger logger, PeriodicStatCollector collector, IMetrics metrics,
               BotConfig config, RedisService redis,
               ErrorMessageService errorMessageService, CommandMessageService commandMessageService,
               Cluster cluster, DiscordApiClient rest, IDiscordCache cache, RuntimeConfigService runtimeConfig)
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
        _redis = redis;
        _cache = cache;
        _runtimeConfig = runtimeConfig;
    }

    private string BotStatus => $"{(_config.Prefixes ?? BotConfig.DefaultPrefixes)[0]}help"
        + (CustomStatusMessage != null ? $" | {CustomStatusMessage}" : "");
    public string CustomStatusMessage = null;

    public void Init()
    {
        _cluster.EventReceived += (shard, evt) => OnEventReceived(shard.ShardId, evt);
        _cluster.DiscordPresence = new GatewayStatusUpdate
        {
            Status = GatewayStatusUpdate.UserStatus.Online,
            Activities = new[]
            {
                new Activity
                {
                    Type = ActivityType.Custom,
                    Name = BotStatus,
                    State = BotStatus
                }
            }
        };

        // Init the shard stuff
        _services.Resolve<ShardInfoService>().Init();

        // Not awaited, just needs to run in the background
        // Trying our best to run it at whole minute boundaries (xx:00), with ~250ms buffer
        // This *probably* doesn't matter in practice but I jut think it's neat, y'know.
        var timeNow = SystemClock.Instance.GetCurrentInstant();
        var timeTillNextWholeMinute = TimeSpan.FromMilliseconds(60000 - timeNow.ToUnixTimeMilliseconds() % 60000 + 250);
        _periodicTask = new Timer(async _ =>
        {
            try
            {
                await UpdatePeriodic();
            }
            catch (Exception e)
            {
                _logger.Error(e, "failed to run once-per-minute scheduled task");
            }
        }, null, timeTillNextWholeMinute, TimeSpan.FromMinutes(1));
    }

    private async Task OnEventReceived(int shardId, IGatewayEvent evt)
    {
        if (_runtimeConfig.Exists("disable_events")) return;

        // we HandleGatewayEvent **before** getting the own user, because the own user is set in HandleGatewayEvent for ReadyEvent
        await _cache.HandleGatewayEvent(evt);
        await _cache.TryUpdateSelfMember(_config.ClientId, evt);
        await OnEventReceivedInner(shardId, evt);
    }

    public async Task OnEventReceivedInner(int shardId, IGatewayEvent evt)
    {
        // HandleEvent takes a type parameter, automatically inferred by the event type
        // It will then look up an IEventHandler<TypeOfEvent> in the DI container and call that object's handler method
        // For registering new ones, see Modules.cs
        if (evt is MessageCreateEvent mc)
            await HandleEvent(shardId, mc);
        if (evt is MessageUpdateEvent mu)
            await HandleEvent(shardId, mu);
        if (evt is MessageDeleteEvent md)
            await HandleEvent(shardId, md);
        if (evt is MessageDeleteBulkEvent mdb)
            await HandleEvent(shardId, mdb);
        if (evt is MessageReactionAddEvent mra)
            await HandleEvent(shardId, mra);
        if (evt is InteractionCreateEvent ic)
            await HandleEvent(shardId, ic);
    }

    public async Task Shutdown()
    {
        // This will stop the timer and prevent any subsequent invocations
        await _periodicTask.DisposeAsync();

        // Send users a lil status message
        // We're not actually properly disconnecting from the gateway (lol)  so it'll linger for a few minutes
        // Should be plenty of time for the bot to connect again next startup and set the real status
        await Task.WhenAll(_cluster.Shards.Values.Select(shard =>
            shard.UpdateStatus(new GatewayStatusUpdate
            {
                Activities = new[]
                {
                    new Activity
                    {
                        Name = "Restarting... (please wait)",
                        State = "Restarting... (please wait)",
                        Type = ActivityType.Custom
                    }
                },
                Status = GatewayStatusUpdate.UserStatus.Idle
            })));
    }

    private Task HandleEvent<T>(int shardId, T evt) where T : IGatewayEvent
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

            using var _ = LogContext.PushProperty("EventId", Guid.NewGuid());
            // this fails when cache lookup fails, so put it in a try-catch
            try
            {
                using var __ = LogContext.Push(await serviceScope.Resolve<SerilogGatewayEnricherFactory>().GetEnricher(shardId, evt));
            }
            catch (Exception exc)
            {

                await HandleError(handler, evt, serviceScope, exc, false);
            }
            _logger.Verbose("Received gateway event: {@Event}", evt);

            try
            {
                var queue = serviceScope.ResolveOptional<HandlerQueue<T>>();

                // Also, find a Sentry enricher for the event type (if one is present), and ask it to put some event data in the Sentry scope
                var sentryEnricher = serviceScope.ResolveOptional<ISentryEnricher<T>>();
                sentryEnricher?.Enrich(serviceScope.Resolve<Scope>(), shardId, evt);

                using var timer = _metrics.Measure.Timer.Time(BotMetrics.EventsHandled,
                    new MetricTags("event", typeof(T).Name.Replace("Event", "")));

                // Delegate to the queue to see if it wants to handle this event
                // the TryHandle call returns true if it's handled the event
                // Usually it won't, so just pass it on to the main handler
                if (queue == null || !await queue.TryHandle(evt))
                    await handler.Handle(shardId, evt);
            }
            catch (Exception exc)
            {
                await HandleError(handler, evt, serviceScope, exc, false);
            }
        }
    }

    public async Task HandleError<T>(IEventHandler<T> handler, T evt, ILifetimeScope serviceScope,
                                      Exception exc, bool preChecksDone)
        where T : IGatewayEvent
    {
        _metrics.Measure.Meter.Mark(BotMetrics.BotErrors, exc.GetType().FullName);

        if (exc is Myriad.Extensions.NotFoundInCacheException ce)
        {
            var scope = serviceScope.Resolve<Scope>();
            scope.SetTag("entity.id", ce.EntityId.ToString());
            scope.SetTag("entity.type", ce.EntityType);
        }

        // Make this beforehand so we can access the event ID for logging
        var sentryEvent = new SentryEvent(exc);

        // If the event is us responding to our own error messages, don't bother logging
        if (evt is MessageCreateEvent mc && mc.Author.Id == _config.ClientId)
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

            // don't show errors for "failed to lookup channel" and such
            // interaction handler doesn't have pre-checks, so just always try to show
            if (!(preChecksDone && !(evt is InteractionCreateEvent _))) return;

            if (!exc.ShowToUser()) return;

            // Once we've sent it to Sentry, report it to the user (if we have permission to)
            var (guildId, reportChannel) = handler.ErrorChannelFor(evt, _config.ClientId);
            if (reportChannel == null)
            {
                if (evt is InteractionCreateEvent ice && ice.Type == Interaction.InteractionType.ApplicationCommand)
                    await _errorMessageService.InteractionRespondWithErrorMessage(ice, sentryEvent.EventId.ToString());
                return;
            }

            var botPerms = await _cache.BotPermissionsIn(guildId ?? 0, reportChannel.Value);
            if (botPerms.HasFlag(PermissionSet.SendMessages | PermissionSet.EmbedLinks))
                await _errorMessageService.SendErrorMessage(reportChannel.Value, sentryEvent.EventId.ToString());
        }
    }

    private async Task UpdatePeriodic()
    {
        _logger.Debug("Running once-per-minute scheduled tasks");

        // Check from a new custom status from Redis and update Discord accordingly
        if (!_config.DisableGateway)
        {
            var newStatus = await _redis.Connection.GetDatabase().StringGetAsync("pluralkit:botstatus");
            if (newStatus != CustomStatusMessage)
            {
                CustomStatusMessage = newStatus;

                _logger.Information("Pushing new bot status message to Discord");
                await Task.WhenAll(_cluster.Shards.Values.Select(shard =>
                    shard.UpdateStatus(new GatewayStatusUpdate
                    {
                        Activities = new[]
                        {
                            new Activity
                            {
                                Name = BotStatus,
                                State = BotStatus,
                                Type = ActivityType.Custom,
                            }
                        },
                        Status = GatewayStatusUpdate.UserStatus.Online
                    })));
            }
        }

        // Collect some stats, submit them to the metrics backend
        await _collector.CollectStats();
        await Task.WhenAll(((IMetricsRoot)_metrics).ReportRunner.RunAllAsync());
        _logger.Debug("Submitted metrics to backend");
    }
}