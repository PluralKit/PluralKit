using PluralKit.Core;
using System.Net;
using System.Net.Http.Json;

namespace PluralKit.Bot;

public class AvatarHostingService
{
    private readonly BotConfig _config;
    private readonly HttpClient _client;

    public AvatarHostingService(BotConfig config)
    {
        _config = config;
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public async Task VerifyAvatarOrThrow(string url, bool isBanner = false)
    {
        if (url.Length > Limits.MaxUriLength)
            throw Errors.UrlTooLong(url);

        if (!PluralKit.Core.MiscUtils.TryMatchUri(url, out var uri))
            throw Errors.InvalidUrl;

        if (uri.Host.Contains("toyhou.se"))
            throw new PKError("Due to server issues, PluralKit is unable to read images hosted on toyhou.se.");

        if (uri.Host == "cdn.pluralkit.me") return;

        if (_config.AvatarServiceUrl == null)
            return;

        var kind = isBanner ? "banner" : "avatar";

        try
        {
            var response = await _client.PostAsJsonAsync(_config.AvatarServiceUrl + "/verify",
                new { url, kind });

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                throw new PKError($"{error.Error}");
            }
        }
        catch (TaskCanceledException e)
        {
            // don't show an internal error to users
            if (e.Message.Contains("HttpClient.Timeout"))
                throw new PKError("Temporary error setting image, please try again later");
            throw;
        }
    }

    public async Task<ParsedImage> TryRehostImage(ParsedImage input, RehostedImageType type, ulong userId, PKSystem? system)
    {
        try
        {
            var uploaded = await TryUploadAvatar(input.Url, type, userId, system);
            if (uploaded != null)
            {
                // todo: make new image type called Cdn?
                return new ParsedImage { Url = uploaded, Source = AvatarSource.HostedCdn };
            }

            return input;
        }
        catch (TaskCanceledException e)
        {
            // don't show an internal error to users
            if (e.Message.Contains("HttpClient.Timeout"))
                throw new PKError("Temporary error setting image, please try again later");
            throw;
        }
    }

    public async Task<string?> TryUploadAvatar(string? avatarUrl, RehostedImageType type, ulong userId, PKSystem? system)
    {
        if (!AvatarUtils.IsDiscordCdnUrl(avatarUrl))
            return null;

        if (_config.AvatarServiceUrl == null)
            return null;

        var kind = type switch
        {
            RehostedImageType.Avatar => "avatar",
            RehostedImageType.Banner => "banner",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        var response = await _client.PostAsJsonAsync(_config.AvatarServiceUrl + "/pull",
            new { url = avatarUrl, kind, uploaded_by = userId, system_id = system?.Uuid.ToString() });
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new PKError($"Error uploading image to CDN: {error.Error}");
        }

        var success = await response.Content.ReadFromJsonAsync<SuccessResponse>();
        return success.Url;
    }

    public record ErrorResponse(string Error);

    public record SuccessResponse(string Url, bool New);

    public enum RehostedImageType
    {
        Avatar,
        Banner,
    }
}