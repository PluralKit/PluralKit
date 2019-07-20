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

                using (SentrySdk.Init(coreConfig.SentryUrl))
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
                MessageCacheSize = 0
            }))
            .AddSingleton<Bot>()

            .AddTransient<CommandService>(_ => new CommandService(new CommandServiceConfig
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
                DefaultRunMode = RunMode.Async
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
            this._services = services;
            this._client = client as DiscordShardedClient;
            this._commands = commands;
            this._proxy = proxy;
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

            // Deliberately wrapping in an async function *without* awaiting, we don't want to "block" since this'd hold up the main loop
            // These handlers return Task so we gotta be careful not to return the Task itself (which would then be awaited) - kinda weird design but eh
            _client.MessageReceived += async (msg) => MessageReceived(msg).CatchException(HandleRuntimeError);
            _client.ReactionAdded += async (message, channel, reaction) => _proxy.HandleReactionAddedAsync(message, channel, reaction).CatchException(HandleRuntimeError);
            _client.MessageDeleted += async (message, channel) => _proxy.HandleMessageDeletedAsync(message, channel).CatchException(HandleRuntimeError);

            _client.Log += FrameworkLog;
        }

        private Task FrameworkLog(LogMessage msg)
        {
            // Bridge D.NET logging to Serilog
            LogEventLevel level = LogEventLevel.Verbose;
            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                    level = LogEventLevel.Fatal;
                    break;
                case LogSeverity.Debug:
                    level = LogEventLevel.Debug;
                    break;
                case LogSeverity.Error:
                    level = LogEventLevel.Error;
                    break;
                case LogSeverity.Info:
                    level = LogEventLevel.Information;
                    break;
                case LogSeverity.Verbose:
                    level = LogEventLevel.Verbose;
                    break;
                case LogSeverity.Warning:
                    level = LogEventLevel.Warning;
                    break;
            }

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

        private async Task ShardReady(DiscordSocketClient shardClient)
        {
            _logger.Information("Shard {Shard} connected", shardClient.ShardId);
            Console.WriteLine($"Shard #{shardClient.ShardId} connected to {shardClient.Guilds.Sum(g => g.Channels.Count)} channels in {shardClient.Guilds.Count} guilds.");

            if (shardClient.ShardId == 0)
            {
                _updateTimer = new Timer((_) => UpdatePeriodic().CatchException(HandleRuntimeError), null, 0, 60*1000);

                Console.WriteLine(
                    $"PluralKit started as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator} ({_client.CurrentUser.Id}).");
            }
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
                        HandleRuntimeError((_result as ExecuteResult?)?.Exception);
                    }
                } else if ((_result.Error == CommandError.BadArgCount || _result.Error == CommandError.MultipleMatches) && cmd.IsSpecified) {
                    await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} {_result.ErrorReason}\n**Usage: **pk;{cmd.Value.Remarks}");
                } else if (_result.Error == CommandError.UnknownCommand || _result.Error == CommandError.UnmetPrecondition || _result.Error == CommandError.ObjectNotFound) {
                    await ctx.Message.Channel.SendMessageAsync($"{Emojis.Error} {_result.ErrorReason}");
                }
            }
        }

        private async Task MessageReceived(SocketMessage _arg)
        {
            _metrics.Measure.Meter.Mark(BotMetrics.MessagesReceived);
            
            // _client.CurrentUser will be null if we've connected *some* shards but not shard #0 yet
            // This will cause an error in WebhookCacheServices so we just workaround and don't process any messages
            // until we properly connect. TODO: can we do this without chucking away a bunch of messages?
            if (_client.CurrentUser == null) return;

            using (var serviceScope = _services.CreateScope())
            {
                SentrySdk.AddBreadcrumb(message: _arg.Content, category: "event.message", data: new Dictionary<string, string>()
                {
                    {"user", _arg.Author.Id.ToString()},
                    {"channel", _arg.Channel.Id.ToString()},
                    {"guild", ((_arg.Channel as IGuildChannel)?.GuildId ?? 0).ToString()}
                });

                // Ignore system messages (member joined, message pinned, etc)
                var arg = _arg as SocketUserMessage;
                if (arg == null) return;
                
                // Ignore bot messages
                if (arg.Author.IsBot || arg.Author.IsWebhook) return;

                int argPos = 0;
                // Check if message starts with the command prefix
                if (arg.HasStringPrefix("pk;", ref argPos, StringComparison.OrdinalIgnoreCase) ||
                    arg.HasStringPrefix("pk!", ref argPos, StringComparison.OrdinalIgnoreCase) ||
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
                    using (var conn = await serviceScope.ServiceProvider.GetService<DbConnectionFactory>().Obtain())
                        system = await conn.QueryFirstOrDefaultAsync<PKSystem>(
                            "select systems.* from systems, accounts where accounts.uid = @Id and systems.id = accounts.system",
                            new {Id = arg.Author.Id});
                    await _commands.ExecuteAsync(new PKCommandContext(_client, arg, system), argPos,
                        serviceScope.ServiceProvider);
                }
                else
                {
                    // If not, try proxying anyway
                    await _proxy.HandleMessageAsync(arg);
                }
            }
        }

        private void HandleRuntimeError(Exception e)
        {
            _logger.Error(e, "Exception in bot event handler");
            SentrySdk.CaptureException(e);
            Console.Error.WriteLine(e);
        }
    }
}