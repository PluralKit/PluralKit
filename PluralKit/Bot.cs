using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.BackendMessages;
using Npgsql.PostgresTypes;
using Npgsql.TypeHandling;
using Npgsql.TypeMapping;
using NpgsqlTypes;

namespace PluralKit
{
    class Initialize
    {
        static void Main() => new Initialize().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            Console.WriteLine("Starting PluralKit...");

            // Dapper by default tries to pass ulongs to Npgsql, which rejects them since PostgreSQL technically
            // doesn't support unsigned types on its own.
            // Instead we add a custom mapper to encode them as signed integers instead, converting them back and forth.
            SqlMapper.RemoveTypeMap(typeof(ulong));
            SqlMapper.AddTypeHandler<ulong>(new UlongEncodeAsLongHandler());
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            using (var services = BuildServiceProvider())
            {
                Console.WriteLine("- Connecting to database...");
                var connection = services.GetRequiredService<IDbConnection>() as NpgsqlConnection;
                connection.ConnectionString = Environment.GetEnvironmentVariable("PK_DATABASE_URI");
                await connection.OpenAsync();

                Console.WriteLine("- Connecting to Discord...");
                var client = services.GetRequiredService<IDiscordClient>() as DiscordSocketClient;
                await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("PK_TOKEN"));
                await client.StartAsync();

                Console.WriteLine("- Initializing bot...");
                await services.GetRequiredService<Bot>().Init();
                
                await Task.Delay(-1);
            }
        }

        public ServiceProvider BuildServiceProvider() => new ServiceCollection()
                .AddSingleton<IDiscordClient, DiscordSocketClient>()
                .AddSingleton<IDbConnection, NpgsqlConnection>()
                .AddSingleton<Bot>()

                .AddSingleton<CommandService>()
                .AddSingleton<LogChannelService>()
                .AddSingleton<ProxyService>()
                
                .AddSingleton<SystemStore>()
                .AddSingleton<MemberStore>()
                .AddSingleton<MessageStore>()
                .BuildServiceProvider();
    }


    class Bot
    {
        private IServiceProvider _services;
        private DiscordSocketClient _client;
        private CommandService _commands;
        private IDbConnection _connection;
        private ProxyService _proxy;

        public Bot(IServiceProvider services, IDiscordClient client, CommandService commands, IDbConnection connection, ProxyService proxy)
        {
            this._services = services;
            this._client = client as DiscordSocketClient;
            this._commands = commands;
            this._connection = connection;
            this._proxy = proxy;
        }

        public async Task Init()
        {
            _commands.AddTypeReader<PKSystem>(new PKSystemTypeReader());
            _commands.AddTypeReader<PKMember>(new PKMemberTypeReader());
            _commands.CommandExecuted += CommandExecuted;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            _client.Ready += Ready;
            _client.MessageReceived += MessageReceived;
            _client.ReactionAdded += _proxy.HandleReactionAddedAsync;
            _client.MessageDeleted += _proxy.HandleMessageDeletedAsync;
        }

        private Task Ready()
        {
            Console.WriteLine($"Shard #{_client.ShardId} connected to {_client.Guilds.Sum(g => g.Channels.Count)} channels in {_client.Guilds.Count} guilds.");
            Console.WriteLine($"PluralKit started as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator} ({_client.CurrentUser.Id}).");
            return Task.CompletedTask;
        }

        private async Task CommandExecuted(Optional<CommandInfo> cmd, ICommandContext ctx, IResult _result)
        {
            if (!_result.IsSuccess) {
                await ctx.Message.Channel.SendMessageAsync("\u274C " + _result.ErrorReason);
            }
        }

        private async Task MessageReceived(SocketMessage _arg)
        {
            try {
                // Ignore system messages (member joined, message pinned, etc)
                var arg = _arg as SocketUserMessage;
                if (arg == null) return;

                // Ignore bot messages
                if (arg.Author.IsBot || arg.Author.IsWebhook) return;

                int argPos = 0;
                // Check if message starts with the command prefix
                if (arg.HasStringPrefix("pk;", ref argPos) || arg.HasStringPrefix("pk!", ref argPos) || arg.HasMentionPrefix(_client.CurrentUser, ref argPos))
                {
                    // If it does, fetch the sender's system (because most commands need that) into the context,
                    // and start command execution
                    var system = await _connection.QueryFirstAsync<PKSystem>("select systems.* from systems, accounts where accounts.uid = @Id and systems.id = accounts.system", new { Id = arg.Author.Id });
                        await _commands.ExecuteAsync(new PKCommandContext(_client, arg as SocketUserMessage, _connection, system), argPos, _services);
    
                }
                else
                {
                    // If not, try proxying anyway
                    await _proxy.HandleMessageAsync(arg);
                }
            } catch (Exception e) {
                // Generic exception handler
                HandleRuntimeError(_arg, e);
            }
        }

        private void HandleRuntimeError(SocketMessage arg, Exception e)
        {
            Console.Error.WriteLine(e);
        }
    }
}