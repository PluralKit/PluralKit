using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Discord;
using Discord.Commands;
using Discord.Commands.Builders;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace PluralKit.Bot
{
    public static class Utils {
        public static string NameAndMention(this IUser user) {
            return $"{user.Username}#{user.Discriminator} ({user.Mention})";
        }
    }

    class UlongEncodeAsLongHandler : SqlMapper.TypeHandler<ulong>
    {
        public override ulong Parse(object value)
        {
            // Cast to long to unbox, then to ulong (???)
            return (ulong)(long)value;
        }

        public override void SetValue(IDbDataParameter parameter, ulong value)
        {
            parameter.Value = (long)value;
        }
    }

    class PKSystemTypeReader : TypeReader
    {
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            var client = services.GetService<IDiscordClient>();
            var conn = services.GetService<IDbConnection>();

            // System references can take three forms:
            // - The direct user ID of an account connected to the system
            // - A @mention of an account connected to the system (<@uid>)
            // - A system hid

            // First, try direct user ID parsing
            if (ulong.TryParse(input, out var idFromNumber)) return await FindSystemByAccountHelper(idFromNumber, client, conn);

            // Then, try mention parsing.
            if (MentionUtils.TryParseUser(input, out var idFromMention)) return await FindSystemByAccountHelper(idFromMention, client, conn);

            // Finally, try HID parsing
            var res = await conn.QuerySingleOrDefaultAsync<PKSystem>("select * from systems where hid = @Hid", new { Hid = input });
            if (res != null) return TypeReaderResult.FromSuccess(res);
            return TypeReaderResult.FromError(CommandError.ObjectNotFound, $"System with ID `{input}` not found.");
        }

        async Task<TypeReaderResult> FindSystemByAccountHelper(ulong id, IDiscordClient client, IDbConnection conn)
        {
            var foundByAccountId = await conn.QuerySingleOrDefaultAsync<PKSystem>("select * from accounts, systems where accounts.system = system.id and accounts.id = @Id", new { Id = id });
            if (foundByAccountId != null) return TypeReaderResult.FromSuccess(foundByAccountId);

            // We didn't find any, so we try to resolve the user ID to find the associated account,
            // so we can print their username.
            var user = await client.GetUserAsync(id);

            // Return descriptive errors based on whether we found the user or not.
            if (user == null) return TypeReaderResult.FromError(CommandError.ObjectNotFound, $"System or account with ID `{id}` not found.");
            return TypeReaderResult.FromError(CommandError.ObjectNotFound, $"Account **{user.Username}#{user.Discriminator}** not found.");
        }
    }

    class PKMemberTypeReader : TypeReader
    {
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            var conn = services.GetService(typeof(IDbConnection)) as IDbConnection;

            // If the sender of the command is in a system themselves,
            // then try searching by the member's name
            if (context is PKCommandContext ctx && ctx.SenderSystem != null)
            {
                var foundByName = await conn.QuerySingleOrDefaultAsync<PKMember>("select * from members where system = @System and lower(name) = lower(@Name)", new { System = ctx.SenderSystem.Id, Name = input });
                if (foundByName != null) return TypeReaderResult.FromSuccess(foundByName);
            }

            // Otherwise, if sender isn't in a system, or no member found by that name,
            // do a standard by-hid search.
            var foundByHid = await conn.QuerySingleOrDefaultAsync<PKMember>("select * from members where hid = @Hid", new { Hid = input });
            if (foundByHid != null) return TypeReaderResult.FromSuccess(foundByHid);
            return TypeReaderResult.FromError(CommandError.ObjectNotFound, "Member not found.");
        }
    }

    /// Subclass of ICommandContext with PK-specific additional fields and functionality
    public class PKCommandContext : SocketCommandContext, ICommandContext
    {
        public IDbConnection Connection { get; }
        public PKSystem SenderSystem { get; }
        
        private object _entity;

        public PKCommandContext(DiscordSocketClient client, SocketUserMessage msg, IDbConnection connection, PKSystem system) : base(client, msg)
        {
            Connection = connection;
            SenderSystem = system;
        }

        public T GetContextEntity<T>() where T: class  {
            return _entity as T;
        }

        public void SetContextEntity(object entity) {
            _entity = entity;
        }
    }

    public abstract class ContextParameterModuleBase<T> : ModuleBase<PKCommandContext> where T: class
    {
        public IServiceProvider _services { get; set; }
        public CommandService _commands { get; set; }

        public abstract string Prefix { get; }
        public abstract Task<T> ReadContextParameterAsync(string value);

        public T ContextEntity => Context.GetContextEntity<T>();

        protected override void OnModuleBuilding(CommandService commandService, ModuleBuilder builder) {
            // We create a catch-all command that intercepts the first argument, tries to parse it as
            // the context parameter, then runs the command service AGAIN with that given in a wrapped
            // context, with the context argument removed so it delegates to the subcommand executor
            builder.AddCommand("", async (ctx, param, services, info) => {
                var pkCtx = ctx as PKCommandContext;
                var res = await ReadContextParameterAsync(param[0] as string);
                pkCtx.SetContextEntity(res);

                await commandService.ExecuteAsync(pkCtx, Prefix + " " + param[1] as string, services);
            }, (cb) => {
                cb.WithPriority(-9999);
                cb.AddPrecondition(new ContextParameterFallbackPreconditionAttribute());
                cb.AddParameter<string>("contextValue", (pb) => pb.WithDefault(""));
                cb.AddParameter<string>("rest", (pb) => pb.WithDefault("").WithIsRemainder(true));
            });
        }
    }

    public class ContextParameterFallbackPreconditionAttribute : PreconditionAttribute
    {
        public ContextParameterFallbackPreconditionAttribute()
        {
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.GetType().Name != "ContextualContext`1") {
                return PreconditionResult.FromSuccess();
            } else {
                return PreconditionResult.FromError("");
            }
        }
    }

    public static class ContextExt {
        public static async Task<bool> PromptYesNo(this ICommandContext ctx, IMessage message, TimeSpan? timeout = null) {
            await ctx.Message.AddReactionsAsync(new[] {new Emoji(Emojis.Success), new Emoji(Emojis.Error)});
            var reaction = await ctx.WaitForReaction(ctx.Message, message.Author, (r) => r.Emote.Name == Emojis.Success || r.Emote.Name == Emojis.Error);
            return reaction.Emote.Name == Emojis.Success;
        }

        public static async Task<SocketReaction> WaitForReaction(this ICommandContext ctx, IUserMessage message, IUser user = null, Func<SocketReaction, bool> predicate = null, TimeSpan? timeout = null) {
            var tcs = new TaskCompletionSource<SocketReaction>();

            Task Inner(Cacheable<IUserMessage, ulong> _message, ISocketMessageChannel _channel, SocketReaction reaction) {
                // Ignore reactions for different messages
                if (message.Id != _message.Id) return Task.CompletedTask;

                // Ignore messages from other users if a user was defined
                if (user != null && user.Id != reaction.UserId) return Task.CompletedTask;
                
                // Check the predicate, if true - accept the reaction
                if (predicate?.Invoke(reaction) ?? true) {
                    tcs.SetResult(reaction);
                }
                return Task.CompletedTask;
            }

            (ctx as BaseSocketClient).ReactionAdded += Inner;

            try {
                return await (tcs.Task.TimeoutAfter(timeout));
            } finally {
                (ctx as BaseSocketClient).ReactionAdded -= Inner;
            }
        }
    }
    class PKError : Exception
    {
        public PKError(string message) : base(message)
        {
        }
    }
}