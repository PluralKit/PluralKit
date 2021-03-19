using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using App.Metrics;

using Autofac;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

using Sentry;

using Serilog;
using Serilog.Context;

namespace PluralKit.Bot
{
    public class Bot
    {
        private readonly ConcurrentDictionary<ulong, GuildMemberPartial> _guildMembers = new();
        
        private readonly Cluster _cluster;
        private readonly DiscordApiClient _rest;
        private readonly ILogger _logger;
        private readonly ILifetimeScope _services;
        private readonly PeriodicStatCollector _collector;
        private readonly IMetrics _metrics;
        private readonly ErrorMessageService _errorMessageService;
        private readonly CommandMessageService _commandMessageService;
        private readonly IDiscordCache _cache;

        private bool _hasReceivedReady = false;
        private Timer _periodicTask; // Never read, just kept here for GC reasons

        public Bot(ILifetimeScope services, ILogger logger, PeriodicStatCollector collector, IMetrics metrics, 
            ErrorMessageService errorMessageService, CommandMessageService commandMessageService, Cluster cluster, DiscordApiClient rest, IDiscordCache cache)
        {
            _logger = logger.ForContext<Bot>();
            _services = services;
            _collector = collector;
            _metrics = metrics;
            _errorMessageService = errorMessageService;
            _commandMessageService = commandMessageService;
            _cluster = cluster;
            _rest = rest;
            _cache = cache;
        }

        public async Task Init()
        {
            _cluster.EventReceived += OnEventReceived;
            
            // Init the shard stuff
            _services.Resolve<ShardInfoService>().Init();

            // Init command reference
            await _services.Resolve<CommandReferenceStore>().Init();

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

        public PermissionSet PermissionsIn(ulong channelId)
        {
            var channel = _cache.GetChannel(channelId);

            if (channel.GuildId != null)
            {
                var member = _guildMembers.GetValueOrDefault(channel.GuildId.Value);
                return _cache.PermissionsFor(channelId, _cluster.User?.Id ?? default, member?.Roles);
            }

            return PermissionSet.Dm;
        }
        
        private async Task OnEventReceived(Shard shard, IGatewayEvent evt)
        {
            await _cache.HandleGatewayEvent(evt);

            TryUpdateSelfMember(shard, evt);

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

            // Update shard status for shards immediately on connect
            if (evt is ReadyEvent re)
                await HandleReady(shard, re);
            if (evt is ResumedEvent)
                await HandleResumed(shard);
        }

        private void TryUpdateSelfMember(Shard shard, IGatewayEvent evt)
        {
            if (evt is GuildCreateEvent gc)
                _guildMembers[gc.Id] = gc.Members.FirstOrDefault(m => m.User.Id == shard.User?.Id);
            if (evt is MessageCreateEvent mc && mc.Member != null && mc.Author.Id == shard.User?.Id)
                _guildMembers[mc.GuildId!.Value] = mc.Member;
            if (evt is GuildMemberAddEvent gma && gma.User.Id == shard.User?.Id)
                _guildMembers[gma.GuildId] = gma;
            if (evt is GuildMemberUpdateEvent gmu && gmu.User.Id == shard.User?.Id)
                _guildMembers[gmu.GuildId] = gmu;
        }

        private Task HandleResumed(Shard shard)
        {
            return UpdateBotStatus(shard);
        }

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
            {
                await Task.WhenAll(_cluster.Shards.Values.Select(shard =>
                    shard.UpdateStatus(new GatewayStatusUpdate
                    {
                        Activities = new[]
                        {
                            new ActivityPartial
                            {
                                Name = "Restarting... (please wait)", 
                                Type = ActivityType.Game
                            }
                        },
                        Status = GatewayStatusUpdate.UserStatus.Idle
                    })));
            }
        }

        private Task HandleEvent<T>(Shard shard, T evt) where T: IGatewayEvent
        {
            // We don't want to stall the event pipeline, so we'll "fork" inside here
            var _ = HandleEventInner();
            return Task.CompletedTask;

            async Task HandleEventInner()
            {
                await Task.Yield();
                
                using var _ = LogContext.PushProperty("EventId", Guid.NewGuid());
                _logger
                    .ForContext("Elastic", "yes?")
                    .Verbose("Gateway event: {@Event}", evt);
                
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
        
        private async Task HandleError<T>(Shard shard, IEventHandler<T> handler, T evt, ILifetimeScope serviceScope, Exception exc)
            where T: IGatewayEvent
        {
            _metrics.Measure.Meter.Mark(BotMetrics.BotErrors, exc.GetType().FullName);
            
            // Make this beforehand so we can access the event ID for logging
            var sentryEvent = new SentryEvent(exc);

            _logger
                .ForContext("Elastic", "yes?")
                .Error(exc, "Exception in event handler: {SentryEventId}", sentryEvent.EventId);

            // If the event is us responding to our own error messages, don't bother logging
            if (evt is MessageCreateEvent mc && mc.Author.Id == shard.User?.Id)
                return;

            var shouldReport = exc.IsOurProblem();
            if (shouldReport)
            {
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

                // Once we've sent it to Sentry, report it to the user (if we have permission to)
                var reportChannel = handler.ErrorChannelFor(evt);
                if (reportChannel != null)
                {
                    var botPerms = PermissionsIn(reportChannel.Value);
                    if (botPerms.HasFlag(PermissionSet.SendMessages | PermissionSet.EmbedLinks))
                        await _errorMessageService.SendErrorMessage(reportChannel.Value, sentryEvent.EventId.ToString());
                }
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
                            new ActivityPartial
                            {
                                Name = $"pk;help | in {totalGuilds} servers | shard #{shard.ShardInfo?.ShardId}",
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
}