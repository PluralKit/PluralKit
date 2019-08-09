using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using Dapper;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Sentry;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;

namespace PluralKit.Bot
{
    class Initialize
    {
        private IConfiguration _config;
        
        static void Main(string[] args) => new Initialize { _config = InitUtils.BuildConfiguration(args).Build()}.MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
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
            
            using (var services = BuildServiceProvider())
            {
                var logger = services.GetRequiredService<ILogger>().ForContext<Initialize>();
                var coreConfig = services.GetRequiredService<CoreConfig>();
                var botConfig = services.GetRequiredService<BotConfig>();

                using (Sentry.SentrySdk.Init(coreConfig.SentryUrl))
                {

                    logger.Information("Connecting to database");
                    using (var conn = await services.GetRequiredService<DbConnectionFactory>().Obtain())
                        await Schema.CreateTables(conn);

                    logger.Information("Connecting to Discord");
                    var client = services.GetRequiredService<IDiscordClient>() as DiscordShardedClient;
                    await client.LoginAsync(TokenType.Bot, botConfig.Token);

                    logger.Information("Initializing bot");
                    await services.GetRequiredService<Bot>().Init();

                    
                    await client.StartAsync();

                    try
                    {
                        await Task.Delay(-1, token.Token);
                    }
                    catch (TaskCanceledException) { } // We'll just exit normally
                    logger.Information("Shutting down");
                }
            }
        }

        public ServiceProvider BuildServiceProvider() => new ServiceCollection()
            .AddTransient(_ => _config.GetSection("PluralKit").Get<CoreConfig>() ?? new CoreConfig())
            .AddTransient(_ => _config.GetSection("PluralKit").GetSection("Bot").Get<BotConfig>() ?? new BotConfig())

            .AddTransient(svc => new DbConnectionFactory(svc.GetRequiredService<CoreConfig>().Database))

            .AddSingleton<IDiscordClient, DiscordShardedClient>(_ => new DiscordShardedClient(new DiscordSocketConfig
            {
                MessageCacheSize = 0,
                ExclusiveBulkDelete = true
            }))
            .AddSingleton<Bot>()

            .AddSingleton(_ => new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = false,
                QuotationMarkAliasMap = new Dictionary<char, char>
                {
                    {'"', '"'},
                    {'\'', '\''},
                    {'‘', '’'},
                    {'“', '”'},
                    {'„', '‟'},
                },
                // We're already asyncing stuff by forking off at the client event handlers
                // So adding an additional layer of forking is pointless
                // and leads to the service scope being disposed of prematurely
                DefaultRunMode = RunMode.Sync
            }))
            .AddTransient<EmbedService>()
            .AddTransient<ProxyService>()
            .AddTransient<LogChannelService>()
            .AddTransient<DataFileService>()

            .AddSingleton<WebhookCacheService>()

            .AddTransient<SystemStore>()
            .AddTransient<MemberStore>()
            .AddTransient<MessageStore>()
            .AddTransient<SwitchStore>()

            .AddSingleton<IMetrics>(svc =>
            {
                var cfg = svc.GetRequiredService<CoreConfig>();
                var builder = AppMetrics.CreateDefaultBuilder();
                if (cfg.InfluxUrl != null && cfg.InfluxDb != null)
                    builder.Report.ToInfluxDb(cfg.InfluxUrl, cfg.InfluxDb);
                return builder.Build();
            })
            .AddSingleton<PeriodicStatCollector>()
            
            .AddScoped(_ => new Sentry.Scope(null))
            .AddTransient<PKEventHandler>()

            .AddSingleton(svc => InitUtils.InitLogger(svc.GetRequiredService<CoreConfig>(), "bot"))
            .BuildServiceProvider();
    }
    class Bot
    {
        private IServiceProvider _services;
        private DiscordShardedClient _client;
        private CommandService _commands;
        private ProxyService _proxy;
        private Timer _updateTimer;
        private IMetrics _metrics;
        private PeriodicStatCollector _collector;
        private ILogger _logger;

        public Bot(IServiceProvider services, IDiscordClient client, CommandService commands, ProxyService proxy, IMetrics metrics, PeriodicStatCollector collector, ILogger logger)
        {
            _services = services;
            _client = client as DiscordShardedClient;
            _commands = commands;
            _proxy = proxy;
            _metrics = metrics;
            _collector = collector;
            _logger = logger.ForContext<Bot>();
        }

        public async Task Init()
        {
            _commands.AddTypeReader<PKSystem>(new PKSystemTypeReader());
            _commands.AddTypeReader<PKMember>(new PKMemberTypeReader());
            _commands.CommandExecuted += CommandExecuted;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            _client.ShardReady += ShardReady;
            _client.Log += FrameworkLog;
            
            _client.MessageReceived += (msg) => HandleEvent(s => s.AddMessageBreadcrumb(msg), eh => eh.HandleMessage(msg));
            _client.ReactionAdded += (msg, channel, reaction) => HandleEvent(s => s.AddReactionAddedBreadcrumb(msg, channel, reaction), eh => eh.HandleReactionAdded(msg, channel, reaction));
            _client.MessageDeleted += (msg, channel) => HandleEvent(s => s.AddMessageDeleteBreadcrumb(msg, channel), eh => eh.HandleMessageDeleted(msg, channel));
            _client.MessagesBulkDeleted += (msgs, channel) => HandleEvent(s => s.AddMessageBulkDeleteBreadcrumb(msgs, channel), eh => eh.HandleMessagesBulkDelete(msgs, channel));
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
            else if (msg.Severity == LogSeverity.Verbose)
                level = LogEventLevel.Verbose;
            else if (msg.Severity == LogSeverity.Warning)
                level = LogEventLevel.Warning;

            _logger.Write(level, msg.Exception, "Discord.Net {Source}: {Message}", msg.Source, msg.Message);
            return Task.CompletedTask;
        }

        // Method called every 60 seconds
        private async Task UpdatePeriodic()
        {
            // Change bot status
            await _client.SetGameAsync($"{_services.GetRequiredService<BotConfig>().Prefix}help | in {_client.Guilds.Count} servers");
            
            await _collector.CollectStats();
            
            _logger.Information("Submitted metrics to backend");
            await Task.WhenAll(((IMetricsRoot) _metrics).ReportRunner.RunAllAsync());
        }

        private Task ShardReady(DiscordSocketClient shardClient)
        {
            _logger.Information("Shard {Shard} connected", shardClient.ShardId);
            Console.WriteLine($"Shard #{shardClient.ShardId} connected to {shardClient.Guilds.Sum(g => g.Channels.Count)} channels in {shardClient.Guilds.Count} guilds.");

            if (shardClient.ShardId == 0)
            {
                _updateTimer = new Timer((_) => {
                    HandleEvent(s => s.AddPeriodicBreadcrumb(), __ => UpdatePeriodic()); 
                }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

                Console.WriteLine(
                    $"PluralKit started as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator} ({_client.CurrentUser.Id}).");
            }

            return Task.CompletedTask;
        }

        private async Task CommandExecuted(Optional<CommandInfo> cmd, ICommandContext ctx, IResult _result)
        {
            _metrics.Measure.Meter.Mark(BotMetrics.CommandsRun);
            
            // TODO: refactor this entire block, it's fugly.
            if (!_result.IsSuccess) {
                if (_result.Error == CommandError.Unsuccessful || _result.Error == CommandError.Exception) {
                    // If this is a PKError (ie. thrown deliberately), show user facing message
                    // If not, log as error
                    var exception = (_result as ExecuteResult?)?.Exception;
                    if (exception is PKError) {
                        await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} {exception.Message}");
                    } else if (exception is TimeoutException) {
                        await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} Operation timed out. Try being faster next time :)");
                    } else if (_result is PreconditionResult)
                    {
                        await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} {_result.ErrorReason}");
                    } else {
                        HandleRuntimeError((_result as ExecuteResult?)?.Exception, ((PKCommandContext) ctx).ServiceProvider.GetRequiredService<Scope>());
                    }
                } else if ((_result.Error == CommandError.BadArgCount || _result.Error == CommandError.MultipleMatches) && cmd.IsSpecified) {
                    await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} {_result.ErrorReason}\n**Usage: **pk;{cmd.Value.Remarks}");
                } else if (_result.Error == CommandError.UnknownCommand || _result.Error == CommandError.UnmetPrecondition || _result.Error == CommandError.ObjectNotFound) {
                    await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} {_result.ErrorReason}");
                }
            }
        }

        private Task HandleEvent(Action<Scope> breadcrumbFactory, Func<PKEventHandler, Task> handler)
        {
            // Inner function so we can await the handler without stalling the entire pipeline
            async Task Inner()
            {
                // Create a DI scope for this event
                // and log the breadcrumb to the newly created (in-svc-scope) Sentry scope
                using (var scope = _services.CreateScope())
                {
                    var sentryScope = scope.ServiceProvider.GetRequiredService<Scope>();
                    breadcrumbFactory(sentryScope);
                
                    try
                    {
                        await handler(scope.ServiceProvider.GetRequiredService<PKEventHandler>());
                    }
                    catch (Exception e)
                    {
                        HandleRuntimeError(e, sentryScope);
                    }
                }

            }

#pragma warning disable 4014
            Inner();
#pragma warning restore 4014
            return Task.CompletedTask;
        }

        private void HandleRuntimeError(Exception e, Scope scope = null)
        {
            _logger.Error(e, "Exception in bot event handler");
            
            var evt = new SentryEvent(e);
            SentrySdk.CaptureEvent(evt, scope);
            
            Console.Error.WriteLine(e);
        }
    }
    
    class PKEventHandler {
        private CommandService _commands;
        private ProxyService _proxy;
        private ILogger _logger;
        private IMetrics _metrics;
        private DiscordShardedClient _client;
        private DbConnectionFactory _connectionFactory;
        private IServiceProvider _services;

        public PKEventHandler(CommandService commands, ProxyService proxy, ILogger logger, IMetrics metrics, IDiscordClient client, DbConnectionFactory connectionFactory, IServiceProvider services)
        {
            _commands = commands;
            _proxy = proxy;
            _logger = logger;
            _metrics = metrics;
            _client = (DiscordShardedClient) client;
            _connectionFactory = connectionFactory;
            _services = services;
        }

        public async Task HandleMessage(SocketMessage msg)
        {
            _metrics.Measure.Meter.Mark(BotMetrics.MessagesReceived);
            
            // _client.CurrentUser will be null if we've connected *some* shards but not shard #0 yet
            // This will cause an error in WebhookCacheServices so we just workaround and don't process any messages
            // until we properly connect. TODO: can we do this without chucking away a bunch of messages?
            if (_client.CurrentUser == null) return;
            
            // Ignore system messages (member joined, message pinned, etc)
            var arg = msg as SocketUserMessage;
            if (arg == null) return;

            // Ignore bot messages
            if (arg.Author.IsBot || arg.Author.IsWebhook) return;

            int argPos = 0;
            var botConfig = _services.GetRequiredService<BotConfig>();
            // Check if message starts with the command prefix
            if (arg.HasStringPrefix(botConfig.Prefix, ref argPos, StringComparison.OrdinalIgnoreCase) ||
                arg.HasMentionPrefix(_client.CurrentUser, ref argPos))
            {
                // Essentially move the argPos pointer by however much whitespace is at the start of the post-argPos string
                var trimStartLengthDiff = arg.Content.Substring(argPos).Length -
                                          arg.Content.Substring(argPos).TrimStart().Length;
                argPos += trimStartLengthDiff;

                // If it does, fetch the sender's system (because most commands need that) into the context,
                // and start command execution
                // Note system may be null if user has no system, hence `OrDefault`
                PKSystem system;
                using (var conn = await _connectionFactory.Obtain())
                    system = await conn.QueryFirstOrDefaultAsync<PKSystem>(
                        "select systems.* from systems, accounts where accounts.uid = @Id and systems.id = accounts.system",
                        new {Id = arg.Author.Id});
                await _commands.ExecuteAsync(new PKCommandContext(_client, arg, system, _services), argPos,
                    _services);
            }
            else
            {
                // If not, try proxying anyway
                await _proxy.HandleMessageAsync(arg);
            }
        }

        public Task HandleReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel,
            SocketReaction reaction) => _proxy.HandleReactionAddedAsync(message, channel, reaction);

        public Task HandleMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel) =>
            _proxy.HandleMessageDeletedAsync(message, channel);

        public Task HandleMessagesBulkDelete(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
            IMessageChannel channel) => _proxy.HandleMessageBulkDeleteAsync(messages, channel);
    }
}