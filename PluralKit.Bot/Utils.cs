using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Discord;
using Discord.Commands;
using Discord.Commands.Builders;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using PluralKit.Core;
using Image = SixLabors.ImageSharp.Image;

namespace PluralKit.Bot
{
    public static class Utils {
        public static string NameAndMention(this IUser user) {
            return $"{user.Username}#{user.Discriminator} ({user.Mention})";
        }

        public static Color? ToDiscordColor(this string color)
        {
            if (uint.TryParse(color, NumberStyles.HexNumber, null, out var colorInt))
                return new Color(colorInt);
            throw new ArgumentException($"Invalid color string '{color}'.");
        }

        public static async Task VerifyAvatarOrThrow(string url)
        {
            // List of MIME types we consider acceptable
            var acceptableMimeTypes = new[]
            {
                "image/jpeg",
                "image/gif",
                "image/png"
                // TODO: add image/webp once ImageSharp supports this
            };

            using (var client = new HttpClient())
            {
                Uri uri;
                try
                {
                    uri = new Uri(url);
                    if (!uri.IsAbsoluteUri) throw Errors.InvalidUrl(url);
                }
                catch (UriFormatException)
                {
                    throw Errors.InvalidUrl(url);
                }
                
                var response = await client.GetAsync(uri);
                if (!response.IsSuccessStatusCode) // Check status code
                    throw Errors.AvatarServerError(response.StatusCode);
                if (response.Content.Headers.ContentLength == null) // Check presence of content length
                    throw Errors.AvatarNotAnImage(null);
                if (response.Content.Headers.ContentLength > Limits.AvatarFileSizeLimit) // Check content length
                    throw Errors.AvatarFileSizeLimit(response.Content.Headers.ContentLength.Value);
                if (!acceptableMimeTypes.Contains(response.Content.Headers.ContentType.MediaType)) // Check MIME type
                    throw Errors.AvatarNotAnImage(response.Content.Headers.ContentType.MediaType);

                // Parse the image header in a worker
                var stream = await response.Content.ReadAsStreamAsync();
                var image = await Task.Run(() => Image.Identify(stream));
                if (image.Width > Limits.AvatarDimensionLimit || image.Height > Limits.AvatarDimensionLimit) // Check image size 
                    throw Errors.AvatarDimensionsTooLarge(image.Width, image.Height);
            }
        }
        
        public static bool HasMentionPrefix(string content, ref int argPos)
        {
            // Roughly ported from Discord.Commands.MessageExtensions.HasMentionPrefix
            if (string.IsNullOrEmpty(content) || content.Length <= 3 || (content[0] != '<' || content[1] != '@'))
                return false;
            int num = content.IndexOf('>');
            if (num == -1 || content.Length < num + 2 || content[num + 1] != ' ' || !MentionUtils.TryParseUser(content.Substring(0, num + 1), out _))
                return false;
            argPos = num + 2;
            return true;
        }

        public static string Sanitize(this string input) =>
            Regex.Replace(Regex.Replace(input, "<@[!&]?(\\d{17,19})>", "<\\@$1>"), "@(everyone|here)", "@\u200B$1");

        public static async Task<ChannelPermissions> PermissionsIn(this IChannel channel)
        {
            switch (channel)
            {
                case IDMChannel _:
                    return ChannelPermissions.DM;
                case IGroupChannel _:
                    return ChannelPermissions.Group;
                case IGuildChannel gc:
                    var currentUser = await gc.Guild.GetCurrentUserAsync();
                    return currentUser.GetPermissions(gc);
                default:
                    return ChannelPermissions.None;
            }
        }

        public static async Task<bool> HasPermission(this IChannel channel, ChannelPermission permission) =>
            (await PermissionsIn(channel)).Has(permission);
    }

    class PKSystemTypeReader : TypeReader
    {
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            var client = services.GetService<IDiscordClient>();
            var systems = services.GetService<SystemStore>();

            // System references can take three forms:
            // - The direct user ID of an account connected to the system
            // - A @mention of an account connected to the system (<@uid>)
            // - A system hid

            // First, try direct user ID parsing
            if (ulong.TryParse(input, out var idFromNumber)) return await FindSystemByAccountHelper(idFromNumber, client, systems);

            // Then, try mention parsing.
            if (MentionUtils.TryParseUser(input, out var idFromMention)) return await FindSystemByAccountHelper(idFromMention, client, systems);

            // Finally, try HID parsing
            var res = await systems.GetByHid(input);
            if (res != null) return TypeReaderResult.FromSuccess(res);
            return TypeReaderResult.FromError(CommandError.ObjectNotFound, $"System with ID `{input}` not found.");
        }

        async Task<TypeReaderResult> FindSystemByAccountHelper(ulong id, IDiscordClient client, SystemStore systems)
        {
            var foundByAccountId = await systems.GetByAccount(id);
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
            var members = services.GetRequiredService<MemberStore>();

            // If the sender of the command is in a system themselves,
            // then try searching by the member's name
            if (context is PKCommandContext ctx && ctx.SenderSystem != null)
            {
                var foundByName = await members.GetByName(ctx.SenderSystem, input);
                if (foundByName != null) return TypeReaderResult.FromSuccess(foundByName);
            }

            // Otherwise, if sender isn't in a system, or no member found by that name,
            // do a standard by-hid search.
            var foundByHid = await members.GetByHid(input);
            if (foundByHid != null) return TypeReaderResult.FromSuccess(foundByHid);
            return TypeReaderResult.FromError(CommandError.ObjectNotFound, $"Member '{input}' not found.");
        }
    }

    /// Subclass of ICommandContext with PK-specific additional fields and functionality
    public class PKCommandContext : SocketCommandContext
    {
        public PKSystem SenderSystem { get; }
        
        private object _entity;

        public PKCommandContext(DiscordSocketClient client, SocketUserMessage msg, PKSystem system) : base(client, msg)
        {
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
        public abstract string ContextNoun { get; }
        public abstract Task<T> ReadContextParameterAsync(string value);

        public T ContextEntity => Context.GetContextEntity<T>();

        protected override void OnModuleBuilding(CommandService commandService, ModuleBuilder builder) {
            // We create a catch-all command that intercepts the first argument, tries to parse it as
            // the context parameter, then runs the command service AGAIN with that given in a wrapped
            // context, with the context argument removed so it delegates to the subcommand executor
            builder.AddCommand("", async (ctx, param, services, info) => {
                var pkCtx = ctx as PKCommandContext;
                pkCtx.SetContextEntity(param[0] as T);

                await commandService.ExecuteAsync(pkCtx, Prefix + " " + param[1] as string, services);
            }, (cb) => {
                cb.WithPriority(-9999);
                cb.AddPrecondition(new MustNotHaveContextPrecondition());
                cb.AddParameter<T>("contextValue", (pb) => pb.WithDefault(""));
                cb.AddParameter<string>("rest", (pb) => pb.WithDefault("").WithIsRemainder(true));
            });
        }
    }

    public class MustNotHaveContextPrecondition : PreconditionAttribute
    {
        public MustNotHaveContextPrecondition()
        {
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            // This stops the "delegating command" we define above from being called multiple times
            // If we've already added a context object to the context, then we'll return with the same
            // error you get when there's an invalid command - it's like it didn't exist
            // This makes sure the user gets the proper error, instead of the command trying to parse things weirdly
            if ((context as PKCommandContext)?.GetContextEntity<object>() == null) return PreconditionResult.FromSuccess();
            return PreconditionResult.FromError(command.Module.Service.Search("<unknown>"));
        }
    }

    public class PKError : Exception
    {
        public PKError(string message) : base(message)
        {
        }
    }

    public class PKSyntaxError : PKError
    {
        public PKSyntaxError(string message) : base(message)
        {
        }
    }
}