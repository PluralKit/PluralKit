using System.Text;
using System.Text.RegularExpressions;

using Myriad.Builders;
using Myriad.Extensions;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using NodaTime;

using SqlKata;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Api
{
    private record PaginatedApiKey(Guid Id, string Name, string[] Scopes, string? AppName, Instant Created);

    private static readonly Regex _webhookRegex =
        new("https://(?:\\w+.)?discord(?:app)?.com/api(?:/v.*)?/webhooks/(.*)");

    private readonly BotConfig _botConfig;
    private readonly DispatchService _dispatch;
    private readonly InteractionDispatchService _interactions;
    private readonly PrivateChannelService _dmCache;
    private readonly ApiKeyService _apiKey;

    public Api(BotConfig botConfig, DispatchService dispatch, InteractionDispatchService interactions, PrivateChannelService dmCache, ApiKeyService apiKey)
    {
        _botConfig = botConfig;
        _dispatch = dispatch;
        _interactions = interactions;
        _dmCache = dmCache;
        _apiKey = apiKey;
    }

    public async Task GetToken(Context ctx)
    {
        ctx.CheckSystem();

        // Get or make a token
        var token = ctx.System.Token ?? await MakeAndSetNewToken(ctx, ctx.System);

        try
        {
            // DM the user a security disclaimer, and then the token in a separate message (for easy copying on mobile)
            var dm = await _dmCache.GetOrCreateDmChannel(ctx.Author.Id);
            await ctx.Rest.CreateMessage(dm,
                new MessageRequest
                {
                    Content = $"{Emojis.Warn} Please note that this grants access to modify (and delete!) all your system data, so keep it safe and secure."
                            + $" If it leaks or you need a new one, you can invalidate this one with `{ctx.DefaultPrefix}token refresh`.\n\nYour token is below:"
                });
            await ctx.Rest.CreateMessage(dm, new MessageRequest { Content = token });

            if (_botConfig.IsBetaBot)
                await ctx.Rest.CreateMessage(dm, new MessageRequest
                {
                    Content = $"{Emojis.Note} The beta bot's API base URL is currently <{_botConfig.BetaBotAPIUrl}>."
                                                                                    + " You need to use this URL instead of the base URL listed on the documentation website."
                });

            // If we're not already in a DM, reply with a reminder to check
            if (ctx.Channel.Type != Channel.ChannelType.Dm)
                await ctx.Reply($"{Emojis.Success} Check your DMs!");
        }
        catch (ForbiddenException)
        {
            // Can't check for permission errors beforehand, so have to handle here :/
            if (ctx.Channel.Type != Channel.ChannelType.Dm)
                await ctx.Reply($"{Emojis.Error} Could not send token in DMs. Are your DMs closed?");
        }
    }

    private async Task<string> MakeAndSetNewToken(Context ctx, PKSystem system)
    {
        system = await ctx.Repository.UpdateSystem(system.Id, new SystemPatch { Token = StringUtils.GenerateToken() });
        return system.Token;
    }

    public async Task RefreshToken(Context ctx)
    {
        ctx.CheckSystem();

        if (ctx.System.Token == null)
        {
            // If we don't have a token, call the other method instead
            // This does pretty much the same thing, except words the messages more appropriately for that :)
            await GetToken(ctx);
            return;
        }

        try
        {
            // DM the user an invalidation disclaimer, and then the token in a separate message (for easy copying on mobile)
            var dm = await _dmCache.GetOrCreateDmChannel(ctx.Author.Id);
            await ctx.Rest.CreateMessage(dm,
                new MessageRequest
                {
                    Content = $"{Emojis.Warn} Your previous API token has been invalidated. You will need to change it anywhere it's currently used.\n\nYour token is below:"
                });

            // Make the new token after sending the first DM; this ensures if we can't DM, we also don't end up
            // breaking their existing token as a side effect :)
            var token = await MakeAndSetNewToken(ctx, ctx.System);
            await ctx.Rest.CreateMessage(dm, new MessageRequest { Content = token });

            if (_botConfig.IsBetaBot)
                await ctx.Rest.CreateMessage(dm, new MessageRequest
                {
                    Content = $"{Emojis.Note} The beta bot's API base URL is currently <{_botConfig.BetaBotAPIUrl}>."
                                                                                   + " You need to use this URL instead of the base URL listed on the documentation website."
                });

            // If we're not already in a DM, reply with a reminder to check
            if (ctx.Channel.Type != Channel.ChannelType.Dm)
                await ctx.Reply($"{Emojis.Success} Check your DMs!");
        }
        catch (ForbiddenException)
        {
            // Can't check for permission errors beforehand, so have to handle here :/
            if (ctx.Channel.Type != Channel.ChannelType.Dm)
                await ctx.Reply($"{Emojis.Error} Could not send token in DMs. Are your DMs closed?");
        }
    }

    public async Task SystemWebhook(Context ctx)
    {
        ctx.CheckSystem().CheckDMContext();

        if (!ctx.HasNext(false))
        {
            if (ctx.System.WebhookUrl == null)
                await ctx.Reply($"Your system does not have a webhook URL set. Set one with `{ctx.DefaultPrefix}system webhook <url>`!");
            else
                await ctx.Reply($"Your system's webhook URL is <{ctx.System.WebhookUrl}>.");
            return;
        }

        if (ctx.MatchClear() && await ctx.ConfirmClear("your system's webhook URL"))
        {
            await ctx.Repository.UpdateSystem(ctx.System.Id, new SystemPatch { WebhookUrl = null, WebhookToken = null });

            await ctx.Reply($"{Emojis.Success} System webhook URL removed.");
            return;
        }

        var newUrl = ctx.RemainderOrNull();
        if (!await DispatchExt.ValidateUri(newUrl))
            throw new PKError($"The URL {newUrl.AsCode()} is invalid or I cannot access it. Are you sure this is a valid, publicly accessible URL?");

        if (_webhookRegex.IsMatch(newUrl))
            throw new PKError("PluralKit does not currently support setting a Discord webhook URL as your system's webhook URL.");

        var newToken = StringUtils.GenerateToken();

        await ctx.Reply($"{Emojis.Warn} The following token is used to authenticate requests from PluralKit to you."
                        + " If it is exposed publicly, you **must** clear and re-set the webhook URL to get a new token."
                        + "\n\n**Please review the security requirements at <https://pluralkit.me/api/dispatch#security> before continuing.**"
                        + "\n\nWhen the server is correctly validating the token, click or reply 'yes' to continue."
        );
        if (!await ctx.PromptYesNo(newToken, "Continue", matchFlag: false))
            throw Errors.GenericCancelled();

        var status = await _dispatch.TestUrl(ctx.System.Uuid, newUrl, newToken);
        if (status != "OK")
        {
            var message = status switch
            {
                "BadData" => "the webhook url is invalid",
                "NoIPs" => "could not find any valid IP addresses for the provided domain",
                "InvalidIP" => "could not find any valid IP addresses for the provided domain",
                "FetchFailed" => "unable to reach server",
                "TestFailed" => "server failed to validate the signing token",
                _ => $"an unknown error occurred ({status})"
            };
            throw new PKError($"Failed to validate the webhook url: {message}");
        }

        await ctx.Repository.UpdateSystem(ctx.System.Id, new SystemPatch { WebhookUrl = newUrl, WebhookToken = newToken });

        await ctx.Reply($"{Emojis.Success} Successfully the new webhook URL for your system.");
    }

    public async Task ApiKeyCreate(Context ctx)
    {
        if (!ctx.HasNext())
            throw new PKSyntaxError($"An API key name must be provided.");

        var rawScopes = ctx.MatchFlag("scopes", "scope");
        var keyName = ctx.PopArgument();
        List<string> keyScopes = new();

        if (!ctx.HasNext())
            throw new PKSyntaxError($"A list of API key scopes must be provided.");

        var scopestr = ctx.RemainderOrNull()!.NormalizeLineEndSpacing().Trim();
        if (rawScopes)
            keyScopes = scopestr.Split(" ").Distinct().ToList();
        else
            keyScopes.Add(scopestr switch
            {
                "full" => "write:all",
                "read private" => "read:all",
                "read public" => "readpublic:all",
                "identify" => "identify",
                _ => throw new PKError(
                    $"Couldn't find a scope preset named {scopestr}."),
            });

        string? check = null!;
        try
        {
            check = await _apiKey.CreateUserApiKey(ctx.System.Id, keyName, keyScopes.ToArray(), check: true);
            if (check != null)
                throw new PKError("API key validation failed: unknown error");
        }
        catch (Exception ex)
        {
            if (ex.Message.StartsWith("API key"))
                throw new PKError(ex.Message);
            throw;
        }

        async Task cb(InteractionContext ictx)
        {
            if (ictx.User.Id != ctx.Author.Id)
            {
                await ictx.Ignore();
                return;
            }

            var newKey = await _apiKey.CreateUserApiKey(ctx.System.Id, keyName, keyScopes.ToArray());
            await ictx.Reply($"Your new API key is below. You will only be shown this once, so please save it!\n\n||`{newKey}`||");
            await ctx.Rest.EditMessage(ictx.ChannelId, ictx.MessageId!.Value, new MessageEditRequest
            {
                Components = new MessageComponent[] { },
            });
        }

        var content =
            $"Ready to create a new API key named **{keyName}**, "
            + $"with these scopes: {(String.Join(", ", keyScopes.Select(x => x.AsCode())))}\n"
            + "To create this API key, press the button below.";

        await ctx.Rest.CreateMessage(ctx.Channel.Id, new MessageRequest
        {
            Content = content,
            AllowedMentions = new() { Parse = new AllowedMentions.ParseType[] { }, RepliedUser = false },
            Components = new[] {
                new MessageComponent
                {
                    Type = ComponentType.ActionRow,
                    Components = new[]
                    {
                        new MessageComponent
                        {
                            Type = ComponentType.Button,
                            Style = ButtonStyle.Primary,
                            Label = "Create API key",
                            CustomId = _interactions.Register(cb),
                        },
                    }
                }
            },
        });
    }

    public async Task ApiKeyList(Context ctx)
    {
        var keys = await ctx.Repository.GetSystemApiKeys(ctx.System.Id)
            .Select(k => new PaginatedApiKey(k.Id, k.Name, k.Scopes, null, k.Created))
            .ToListAsync();

        await ctx.Paginate<PaginatedApiKey>(
            keys.ToAsyncEnumerable(),
            keys.Count,
            10,
            "Current API keys for your system",
            ctx.System.Color,
            (eb, l) =>
            {
                var description = new StringBuilder();

                foreach (var item in l)
                {
                    description.Append($"**{item.Name}** (`{item.Id}`)");
                    description.AppendLine();

                    description.Append("- Scopes: ");
                    description.Append(String.Join(", ", item.Scopes.Select(sc => $"`{sc}`")));
                    description.AppendLine();
                    description.Append("- Created: ");
                    description.Append(item.Created.FormatZoned(ctx.Zone));
                    description.AppendLine();
                    description.AppendLine();
                }

                eb.Description(description.ToString());
                return Task.CompletedTask;
            }
        );
    }

    public async Task ApiKeyRename(Context ctx, PKApiKey key)
    {
        if (!ctx.HasNext())
            throw new PKError("You must provide a new name for this API key.");

        var name = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();
        await ctx.Repository.UpdateApiKey(key.Id, new ApiKeyPatch { Name = name });
        await ctx.Reply($"{Emojis.Success} API key renamed.");
    }

    public async Task ApiKeyDelete(Context ctx, PKApiKey key)
    {
        if (!await ctx.PromptYesNo($"Really delete API key **{key.Name}** `{key.Id}`?", "Delete", matchFlag: false))
        {
            await ctx.Reply($"{Emojis.Error} Deletion cancelled.");
            return;
        }

        await ctx.Repository.DeleteApiKey(key.Id);
        await ctx.Reply($"{Emojis.Success} Successfully deleted API key.");
    }

    public async Task ApiKeyDeleteAll(Context ctx)
    {
        if (!await ctx.PromptYesNo($"Really delete *all manually-created* API keys for your system?", "Delete", matchFlag: false))
        {
            await ctx.Reply($"{Emojis.Error} Deletion cancelled.");
            return;
        }

        await ctx.BusyIndicator(async () =>
        {
            var query = new Query("api_keys")
                .AsDelete()
                .WhereRaw("[kind]::text not in ( 'dashboard', 'external_app' )")
                .Where("system", ctx.System.Id);

            await ctx.Database.ExecuteQuery(query);
        });

        await ctx.Reply($"{Emojis.Success} Successfully deleted all manually-created API keys.");
    }
}