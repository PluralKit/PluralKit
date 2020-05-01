using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;

using Autofac;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using Microsoft.Extensions.Configuration;

using NodaTime;

using PluralKit.Core;

using Sentry;

using Serilog;
using Serilog.Events;

namespace PluralKit.Bot
{
    class Initialize
    {
        private IConfiguration _config;
        
        static void Main(string[] args) => new Initialize { _config = InitUtils.BuildConfiguration(args).Build()}.MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            ThreadPool.SetMinThreads(32, 32);
            ThreadPool.SetMaxThreads(128, 128);
            
            Console.WriteLine("Starting PluralKit...");
            
            InitUtils.Init();

            // Set up a CancellationToken and a SIGINT hook to properly dispose of things when the app is closed
            // The Task.Delay line will throw/exit (forgot which) and the stack and using statements will properly unwind
            var token = new CancellationTokenSource();
            Console.CancelKeyPress += delegate(object e, ConsoleCancelEventArgs args)
            {
                args.Cancel = true;
                token.Cancel();
            };

            var builder = new ContainerBuilder();
            builder.RegisterInstance(_config);
            builder.RegisterModule(new ConfigModule<BotConfig>("Bot"));
            builder.RegisterModule(new LoggingModule("bot"));
            builder.RegisterModule(new MetricsModule());
            builder.RegisterModule<DataStoreModule>();
            builder.RegisterModule<BotModule>();

            using var services = builder.Build();
            
            var logger = services.Resolve<ILogger>().ForContext<Initialize>();
            
            try
            {
                SchemaService.Initialize();

                var coreConfig = services.Resolve<CoreConfig>();
                var schema = services.Resolve<SchemaService>();

                using var _ = Sentry.SentrySdk.Init(coreConfig.SentryUrl);
                
                logger.Information("Connecting to database");
                await schema.ApplyMigrations();

                logger.Information("Connecting to Discord");
                var client = services.Resolve<DiscordShardedClient>();
                await client.StartAsync();
                
                logger.Information("Initializing bot");
                await services.Resolve<Bot>().Init();
                
                try
                {
                    await Task.Delay(-1, token.Token);
                }
                catch (TaskCanceledException) { } // We'll just exit normally
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Unrecoverable error while initializing bot");
            }

            logger.Information("Shutting down");
            
            // Allow the log buffer to flush properly before exiting (needed for fatal errors)
            await Task.Delay(1000);
        }
    }
    class Bot
    {
        private ILifetimeScope _services;
        private DiscordShardedClient _client;
        private IMetrics _metrics;
        private PeriodicStatCollector _collector;
        private ILogger _logger;
        private Task _periodicWorker;
        
        public Bot(ILifetimeScope services, DiscordShardedClient client, IMetrics metrics, PeriodicStatCollector collector, ILogger logger)
        {
            _services = services;
            _client = client;
            _metrics = metrics;
            _collector = collector;
            _logger = logger.ForContext<Bot>();
        }

        public Task Init()
        {
            _client.DebugLogger.LogMessageReceived += FrameworkLog;
            
            _client.MessageCreated += args => HandleEvent(eh => eh.HandleMessage(args));
            _client.MessageReactionAdded += args => HandleEvent(eh => eh.HandleReactionAdded(args));
            _client.MessageDeleted += args => HandleEvent(eh => eh.HandleMessageDeleted(args));
            _client.MessagesBulkDeleted += args => HandleEvent(eh => eh.HandleMessagesBulkDelete(args));
            _client.MessageUpdated += args => HandleEvent(eh => eh.HandleMessageEdited(args)); 
            
            _services.Resolve<ShardInfoService>().Init(_client);
            
            // Will not be awaited, just runs in the background
            _periodicWorker = UpdatePeriodic();

            return Task.CompletedTask;
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
                    await _client.UpdateStatusAsync(new DiscordActivity($"pk;help | in {totalGuilds} servers"));
                }
                catch (WebSocketException) { }

                await _collector.CollectStats();

                _logger.Information("Submitted metrics to backend");
                await Task.WhenAll(((IMetricsRoot) _metrics).ReportRunner.RunAllAsync());
            }
        }

        private Task HandleEvent(Func<PKEventHandler, Task> handler)
        {
            // Inner function so we can await the handler without stalling the entire pipeline
            async Task Inner()
            {
                // "Fork" this task off by ~~yeeting~~ yielding it at the back of the task queue
                // This prevents any synchronous nonsense from also stalling the pipeline before the first await point
                await Task.Yield();
                
                using var containerScope = _services.BeginLifetimeScope();
                var sentryScope = containerScope.Resolve<Scope>();
                var eventHandler = containerScope.Resolve<PKEventHandler>();

                try
                {
                    await handler(eventHandler);
                }
                catch (Exception e)
                {
                    await HandleRuntimeError(eventHandler, e, sentryScope);
                }
            }

            var _ = Inner();
            return Task.CompletedTask;
        }

        private async Task HandleRuntimeError(PKEventHandler eventHandler, Exception exc, Scope scope)
        {
            _logger.Error(exc, "Exception in bot event handler");
            
            var evt = new SentryEvent(exc);
            
            // Don't blow out our Sentry budget on sporadic not-our-problem erorrs
            if (exc.IsOurProblem())
                SentrySdk.CaptureEvent(evt, scope);
            
            // Once we've sent it to Sentry, report it to the user
            await eventHandler.ReportError(evt, exc);
        }
    }
    
    class PKEventHandler {
        private ProxyService _proxy;
        private ILogger _logger;
        private IMetrics _metrics;
        private DiscordShardedClient _client;
        private DbConnectionFactory _connectionFactory;
        private ILifetimeScope _services;
        private CommandTree _tree;
        private Scope _sentryScope;
        private ProxyCache _cache;
        private LastMessageCacheService _lastMessageCache;
        private LoggerCleanService _loggerClean;

        // We're defining in the Autofac module that this class is instantiated with one instance per event
        // This means that the HandleMessage function will either be called once, or not at all
        // The ReportError function will be called on an error, and needs to refer back to the "trigger message"
        // hence, we just store it in a local variable, ignoring it entirely if it's null.
        private DiscordMessage _currentlyHandlingMessage = null;

        public PKEventHandler(ProxyService proxy, ILogger logger, IMetrics metrics, DiscordShardedClient client, DbConnectionFactory connectionFactory, ILifetimeScope services, CommandTree tree, Scope sentryScope, ProxyCache cache, LastMessageCacheService lastMessageCache, LoggerCleanService loggerClean)
        {
            _proxy = proxy;
            _logger = logger;
            _metrics = metrics;
            _client = client;
            _connectionFactory = connectionFactory;
            _services = services;
            _tree = tree;
            _sentryScope = sentryScope;
            _cache = cache;
            _lastMessageCache = lastMessageCache;
            _loggerClean = loggerClean;
        }

        public async Task HandleMessage(MessageCreateEventArgs args)
        {
            // TODO
            /*var shard = _client.GetShardFor((arg.Channel as IGuildChannel)?.Guild);
            if (shard.ConnectionState != ConnectionState.Connected || _client.CurrentUser == null)
                return; // Discard messages while the bot "catches up" to avoid unnecessary CPU pressure causing timeouts*/

            RegisterMessageMetrics(args);

            // Ignore system messages (member joined, message pinned, etc)
            var msg = args.Message;
            if (msg.MessageType != MessageType.Default) return;
            
            // Fetch information about the guild early, as we need it for the logger cleanup
            GuildConfig cachedGuild = default;
            if (msg.Channel.Type == ChannelType.Text) cachedGuild = await _cache.GetGuildDataCached(msg.Channel.GuildId);
            
            // Pass guild bot/WH messages onto the logger cleanup service, but otherwise ignore
            if (msg.Author.IsBot && msg.Channel.Type == ChannelType.Text)
            {
                await _loggerClean.HandleLoggerBotCleanup(msg, cachedGuild);
                return;
            }

            _currentlyHandlingMessage = msg;
            
            // Add message info as Sentry breadcrumb
            _sentryScope.AddBreadcrumb(msg.Content, "event.message", data: new Dictionary<string, string>
            {
                {"user", msg.Author.Id.ToString()},
                {"channel", msg.Channel.Id.ToString()},
                {"guild", msg.Channel.GuildId.ToString()},
                {"message", msg.Id.ToString()},
            });
            _sentryScope.SetTag("shard", args.Client.ShardId.ToString());
            
            // Add to last message cache
            _lastMessageCache.AddMessage(msg.Channel.Id, msg.Id);
            
            // We fetch information about the sending account from the cache
            var cachedAccount = await _cache.GetAccountDataCached(msg.Author.Id);
            // this ^ may be null, do remember that down the line

            int argPos = -1;
            // Check if message starts with the command prefix
            if (msg.Content.StartsWith("pk;", StringComparison.InvariantCultureIgnoreCase)) argPos = 3;
            else if (msg.Content.StartsWith("pk!", StringComparison.InvariantCultureIgnoreCase)) argPos = 3;
            else if (msg.Content != null && StringUtils.HasMentionPrefix(msg.Content, ref argPos, out var id)) // Set argPos to the proper value
                if (id != _client.CurrentUser.Id) // But undo it if it's someone else's ping
                    argPos = -1;
            
            // If it does, try executing a command
            if (argPos > -1)
            {
                _logger.Verbose("Parsing command {Command} from message {Channel}-{Message}", msg.Content, msg.Channel.Id, msg.Id);
                
                // Essentially move the argPos pointer by however much whitespace is at the start of the post-argPos string
                var trimStartLengthDiff = msg.Content.Substring(argPos).Length -
                                          msg.Content.Substring(argPos).TrimStart().Length;
                argPos += trimStartLengthDiff;

                try
                {
                    await _tree.ExecuteCommand(new Context(_services, args.Client, msg, argPos, cachedAccount?.System));
                }
                catch (PKError)
                {
                    // Only permission errors will ever bubble this far and be caught here instead of Context.Execute
                    // so we just catch and ignore these. TODO: this may need to change.
                }
            }
            else if (cachedAccount != null)
            {
                // If not, try proxying anyway
                // but only if the account data we got before is present
                // no data = no account = no system = no proxy!
                try
                {
                    await _proxy.HandleMessageAsync(args.Client, cachedGuild, cachedAccount, msg, doAutoProxy: true);
                }
                catch (PKError e)
                {
                    if (msg.Channel.Guild == null || msg.Channel.BotHasPermission(Permissions.SendMessages))
                        await msg.Channel.SendMessageAsync($"{Emojis.Error} {e.Message}");
                }
            }
        }

        public async Task ReportError(SentryEvent evt, Exception exc)
        {
            // If we don't have a "trigger message", bail
            if (_currentlyHandlingMessage == null) return;
            
            // This function *specifically* handles reporting a command execution error to the user.
            // We'll fetch the event ID and send a user-facing error message.
            // ONLY IF this error's actually our problem. As for what defines an error as "our problem",
            // check the extension method :)
            if (exc.IsOurProblem() && _currentlyHandlingMessage.Channel.BotHasPermission(Permissions.SendMessages))
            {
                var eid = evt.EventId;
                await _currentlyHandlingMessage.Channel.SendMessageAsync(
                    $"{Emojis.Error} Internal error occurred. Please join the support server (<https://discord.gg/PczBt78>), and send the developer this ID: `{eid}`\nBe sure to include a description of what you were doing to make the error occur.");
            }
            
            // If not, don't care. lol.
        }

        private void RegisterMessageMetrics(MessageCreateEventArgs msg)
        {
            _metrics.Measure.Meter.Mark(BotMetrics.MessagesReceived);

            var gatewayLatency = DateTimeOffset.Now - msg.Message.Timestamp;
            _logger.Verbose("Message received with latency {Latency}", gatewayLatency);
        }

        public Task HandleReactionAdded(MessageReactionAddEventArgs args)
        {
            _sentryScope.AddBreadcrumb("", "event.reaction", data: new Dictionary<string, string>()
            {
                {"user", args.User.Id.ToString()},
                {"channel", (args.Channel?.Id ?? 0).ToString()},
                {"guild", (args.Channel?.GuildId ?? 0).ToString()},
                {"message", args.Message.Id.ToString()},
                {"reaction", args.Emoji.Name}
            });
            _sentryScope.SetTag("shard", args.Client.ShardId.ToString());
            return _proxy.HandleReactionAddedAsync(args);
        }

        public Task HandleMessageDeleted(MessageDeleteEventArgs args)
        {
            _sentryScope.AddBreadcrumb("", "event.messageDelete", data: new Dictionary<string, string>()
            {
                {"channel", args.Channel.Id.ToString()},
                {"guild", args.Channel.GuildId.ToString()},
                {"message", args.Message.Id.ToString()},
            });
            _sentryScope.SetTag("shard", args.Client.ShardId.ToString());

            return _proxy.HandleMessageDeletedAsync(args);
        }

        public Task HandleMessagesBulkDelete(MessageBulkDeleteEventArgs args)
        {
            _sentryScope.AddBreadcrumb("", "event.messageDelete", data: new Dictionary<string, string>()
            {
                {"channel", args.Channel.Id.ToString()},
                {"guild", args.Channel.Id.ToString()},
                {"messages", string.Join(",", args.Messages.Select(m => m.Id))},
            });
            _sentryScope.SetTag("shard", args.Client.ShardId.ToString());

            return _proxy.HandleMessageBulkDeleteAsync(args);
        }

        public async Task HandleMessageEdited(MessageUpdateEventArgs args)
        {
            // Sometimes edit message events arrive for other reasons (eg. an embed gets updated server-side)
            // If this wasn't a *content change* (ie. there's message contents to read), bail
            // It'll also sometimes arrive with no *author*, so we'll go ahead and ignore those messages too
            if (args.Message.Content == null) return;
            if (args.Author == null) return;
            
            _sentryScope.AddBreadcrumb(args.Message.Content ?? "<unknown>", "event.messageEdit", data: new Dictionary<string, string>()
            {
                {"channel", args.Channel.Id.ToString()},
                {"guild", args.Channel.GuildId.ToString()},
                {"message", args.Message.Id.ToString()}
            });
            _sentryScope.SetTag("shard", args.Client.ShardId.ToString());

            // If this isn't a guild, bail
            if (args.Channel.Guild == null) return;
            
            // If this isn't the last message in the channel, don't do anything
            if (_lastMessageCache.GetLastMessage(args.Channel.Id) != args.Message.Id) return;
            
            // Fetch account from cache if there is any
            var account = await _cache.GetAccountDataCached(args.Author.Id);
            if (account == null) return; // Again: no cache = no account = no system = no proxy
            
            // Also fetch guild cache
            var guild = await _cache.GetGuildDataCached(args.Channel.GuildId);

            // Just run the normal message handling stuff
            await _proxy.HandleMessageAsync(args.Client, guild, account, args.Message, doAutoProxy: false);
        }
    }
}
