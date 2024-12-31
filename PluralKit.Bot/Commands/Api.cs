using System.Text.RegularExpressions;

using Myriad.Extensions;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Api
{
    private static readonly Regex _webhookRegex =
        new("https://(?:\\w+.)?discord(?:app)?.com/api(?:/v.*)?/webhooks/(.*)");

    private readonly BotConfig _botConfig;
    private readonly DispatchService _dispatch;
    private readonly PrivateChannelService _dmCache;

    public Api(BotConfig botConfig, DispatchService dispatch, PrivateChannelService dmCache)
    {
        _botConfig = botConfig;
        _dispatch = dispatch;
        _dmCache = dmCache;
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
}