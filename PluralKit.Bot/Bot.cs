using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;

using Autofac;
using Autofac.Core;

using Dapper;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PluralKit.Bot.Commands;
using PluralKit.Bot.CommandSystem;
using PluralKit.Core;

using Sentry;
using Sentry.Infrastructure;

using Serilog;
using Serilog.Events;

using SystemClock = NodaTime.SystemClock;

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
                var botConfig = services.Resolve<BotConfig>();
                var schema = services.Resolve<SchemaService>();

                using var _ = Sentry.SentrySdk.Init(coreConfig.SentryUrl);
                
                logger.Information("Connecting to database");
                await schema.ApplyMigrations();

                logger.Information("Connecting to Discord");
                var client = services.Resolve<DiscordShardedClient>();
                await client.LoginAsync(TokenType.Bot, botConfig.Token);

                logger.Information("Initializing bot");
                await client.StartAsync();
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
        }
    }
    class Bot
    {
        private ILifetimeScope _services;
        private DiscordShardedClient _client;
        private Timer _updateTimer;
        private IMetrics _metrics;
        private PeriodicStatCollector _collector;
        private ILogger _logger;
        private PKPerformanceEventListener _pl;

        public Bot(ILifetimeScope services, IDiscordClient client, IMetrics metrics, PeriodicStatCollector collector, ILogger logger)
        {
            _pl = new PKPerformanceEventListener();
            _services = services;
            _client = client as DiscordShardedClient;
            _metrics = metrics;
            _collector = collector;
            _logger = logger.ForContext<Bot>();
        }

        public Task Init()
        {
            _client.ShardDisconnected += ShardDisconnected;
            _client.ShardReady += ShardReady;
            _client.Log += FrameworkLog;
            
            _client.MessageReceived += (msg) => HandleEvent(eh => eh.HandleMessage(msg));
            _client.ReactionAdded += (msg, channel, reaction) => HandleEvent(eh => eh.HandleReactionAdded(msg, channel, reaction));
            _client.MessageDeleted += (msg, channel) => HandleEvent(eh => eh.HandleMessageDeleted(msg, channel));
            _client.MessagesBulkDeleted += (msgs, channel) => HandleEvent(eh => eh.HandleMessagesBulkDelete(msgs, channel));
            
            _services.Resolve<ShardInfoService>().Init(_client);

            return Task.CompletedTask;
        }

        private Task ShardDisconnected(Exception ex, DiscordSocketClient shard)
        {
            _logger.Warning(ex, $"Shard #{shard.ShardId} disconnected");
            return Task.CompletedTask;
        }

        private Task FrameworkLog(LogMessage msg)
        {
            // Bridge D.NET logging to Serilog
            LogEventLevel level = LogEventLevel.Verbose;
            if (msg.Severity == LogSeverity.Critical)
                level = LogEventLevel.Fatal;
            else if (msg.Severity == LogSeverity.Debug)
                level = LogEventLevel.Debug;
            else if (msg.Severity == LogSeverity.Error)
                level = LogEventLevel.Error;
            else if (msg.Severity == LogSeverity.Info)
                level = LogEventLevel.Information;
            else if (msg.Severity == LogSeverity.Debug) // D.NET's lowest level is Debug and Verbose is greater, Serilog's is the other way around
                level = LogEventLevel.Verbose;
            else if (msg.Severity == LogSeverity.Verbose)
                level = LogEventLevel.Debug;

            _logger.Write(level, msg.Exception, "Discord.Net {Source}: {Message}", msg.Source, msg.Message);
            return Task.CompletedTask;
        }

        // Method called every 60 seconds
        private async Task UpdatePeriodic()
        {
            // Change bot status
            await _client.SetGameAsync($"pk;help | in {_client.Guilds.Count} servers");
            
            await _collector.CollectStats();
            
            _logger.Information("Submitted metrics to backend");
            await Task.WhenAll(((IMetricsRoot) _metrics).ReportRunner.RunAllAsync());
        }

        private Task ShardReady(DiscordSocketClient shardClient)
        {
            _logger.Information("Shard {Shard} connected to {ChannelCount} channels in {GuildCount} guilds", shardClient.ShardId, shardClient.Guilds.Sum(g => g.Channels.Count), shardClient.Guilds.Count);

            if (shardClient.ShardId == 0)
            {
                _updateTimer = new Timer((_) => {
                    HandleEvent(_ => UpdatePeriodic()); 
                }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

                _logger.Information("PluralKit started as {Username}#{Discriminator} ({Id})", _client.CurrentUser.Username, _client.CurrentUser.Discriminator, _client.CurrentUser.Id);
        }

            return Task.CompletedTask;
        }

        private Task HandleEvent(Func<PKEventHandler, Task> handler)
        {
            _logger.Debug("Received event");
            
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

        // We're defining in the Autofac module that this class is instantiated with one instance per event
        // This means that the HandleMessage function will either be called once, or not at all
        // The ReportError function will be called on an error, and needs to refer back to the "trigger message"
        // hence, we just store it in a local variable, ignoring it entirely if it's null.
        private IUserMessage _msg = null;

        public PKEventHandler(ProxyService proxy, ILogger logger, IMetrics metrics, DiscordShardedClient client, DbConnectionFactory connectionFactory, ILifetimeScope services, CommandTree tree, Scope sentryScope)
        {
            _proxy = proxy;
            _logger = logger;
            _metrics = metrics;
            _client = client;
            _connectionFactory = connectionFactory;
            _services = services;
            _tree = tree;
            _sentryScope = sentryScope;
        }

        public async Task HandleMessage(SocketMessage arg)
        {
            if (_client.GetShardFor((arg.Channel as IGuildChannel)?.Guild).ConnectionState != ConnectionState.Connected)
                return; // Discard messages while the bot "catches up" to avoid unnecessary CPU pressure causing timeouts

            RegisterMessageMetrics(arg);

            // Ignore system messages (member joined, message pinned, etc)
            var msg = arg as SocketUserMessage;
            if (msg == null) return;

            // Ignore bot messages
            if (msg.Author.IsBot || msg.Author.IsWebhook) return;
            
            // Add message info as Sentry breadcrumb
            _msg = msg;
            _sentryScope.AddBreadcrumb(msg.Content, "event.message", data: new Dictionary<string, string>
            {
                {"user", msg.Author.Id.ToString()},
                {"channel", msg.Channel.Id.ToString()},
                {"guild", ((msg.Channel as IGuildChannel)?.GuildId ?? 0).ToString()},
                {"message", msg.Id.ToString()},
            });
            
            int argPos = -1;
            // Check if message starts with the command prefix
            if (msg.Content.StartsWith("pk;", StringComparison.InvariantCultureIgnoreCase)) argPos = 3;
            else if (msg.Content.StartsWith("pk!", StringComparison.InvariantCultureIgnoreCase)) argPos = 3;
            else if (msg.Content != null && Utils.HasMentionPrefix(msg.Content, ref argPos, out var id)) // Set argPos to the proper value
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

                // If it does, fetch the sender's system (because most commands need that) into the context,
                // and start command execution
                // Note system may be null if user has no system, hence `OrDefault`
                PKSystem system;
                using (var conn = await _connectionFactory.Obtain())
                    system = await conn.QueryFirstOrDefaultAsync<PKSystem>(
                        "select systems.* from systems, accounts where accounts.uid = @Id and systems.id = accounts.system",
                        new {Id = msg.Author.Id});
                
                await _tree.ExecuteCommand(new Context(_services, msg, argPos, system));
            }
            else
            {
                // If not, try proxying anyway
                try
                {
                    await _proxy.HandleMessageAsync(msg);
                }
                catch (PKError e)
                {
                    await arg.Channel.SendMessageAsync($"{Emojis.Error} {e.Message}");
                }
            }
        }

        public async Task ReportError(SentryEvent evt, Exception exc)
        {
            // If we don't have a "trigger message", bail
            if (_msg == null) return;
            
            // This function *specifically* handles reporting a command execution error to the user.
            // We'll fetch the event ID and send a user-facing error message.
            // ONLY IF this error's actually our problem. As for what defines an error as "our problem",
            // check the extension method :)
            if (exc.IsOurProblem())
            {
                var eid = evt.EventId;
                await _msg.Channel.SendMessageAsync(
                    $"{Emojis.Error} Internal error occurred. Please join the support server (<https://discord.gg/PczBt78>), and send the developer this ID: `{eid}`\nBe sure to include a description of what you were doing to make the error occur.");
            }
            
            // If not, don't care. lol.
        }

        private void RegisterMessageMetrics(SocketMessage msg)
        {
            _metrics.Measure.Meter.Mark(BotMetrics.MessagesReceived);

            var gatewayLatency = DateTimeOffset.Now - msg.CreatedAt;
            _logger.Verbose("Message received with latency {Latency}", gatewayLatency);
        }

        public Task HandleReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel,
            SocketReaction reaction)
        {
            _sentryScope.AddBreadcrumb("", "event.reaction", data: new Dictionary<string, string>()
            {
                {"user", reaction.UserId.ToString()},
                {"channel", channel.Id.ToString()},
                {"guild", ((channel as IGuildChannel)?.GuildId ?? 0).ToString()},
                {"message", message.Id.ToString()},
                {"reaction", reaction.Emote.Name}
            });
            
            return _proxy.HandleReactionAddedAsync(message, channel, reaction);
        }

        public Task HandleMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            _sentryScope.AddBreadcrumb("", "event.messageDelete", data: new Dictionary<string, string>()
            {
                {"channel", channel.Id.ToString()},
                {"guild", ((channel as IGuildChannel)?.GuildId ?? 0).ToString()},
                {"message", message.Id.ToString()},
            });
            
            return _proxy.HandleMessageDeletedAsync(message, channel);
        }

        public Task HandleMessagesBulkDelete(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
                                             IMessageChannel channel)
        {
            _sentryScope.AddBreadcrumb("", "event.messageDelete", data: new Dictionary<string, string>()
            {
                {"channel", channel.Id.ToString()},
                {"guild", ((channel as IGuildChannel)?.GuildId ?? 0).ToString()},
                {"messages", string.Join(",", messages.Select(m => m.Id))},
            });
            
            return _proxy.HandleMessageBulkDeleteAsync(messages, channel);
        }
    }
}
