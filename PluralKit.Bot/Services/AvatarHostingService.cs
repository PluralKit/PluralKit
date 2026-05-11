using PluralKit.Core;
using Serilog;
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
        _client.DefaultRequestHeaders.Add("User-Agent", "pluralkit-dotnet-bot");
        if (_config.InternalAuthToken != null)
            _client.DefaultRequestHeaders.Add("x-pluralkit-internalauth", _config.InternalAuthToken);
    }

    public Task<HttpResponseMessage> RequestApi(String path, SystemId system, object data)
    {
        var request = new HttpRequestMessage
        {
            RequestUri = new Uri(_config.ApiInternalUrl + path),
            Method = HttpMethod.Post,
            Content = JsonContent.Create(data),
        };

        request.Headers.Add("x-pluralkit-systemid", system.Value.ToString());

        return _client.SendAsync(request);
    }

    public async Task<ParsedImage> VerifyAndRehostImage(Context ctx, ParsedImage input, RehostedImageType type)
    {
        var url = input.Url;

        if (url.Length > Limits.MaxUriLength)
            throw Errors.UrlTooLong(url);

        if (!PluralKit.Core.MiscUtils.TryMatchUri(url, out var uri))
            throw Errors.InvalidUrl;

        if (uri.Host.Contains("toyhou.se"))
            throw new PKError("Due to server issues, PluralKit is unable to read images hosted on toyhou.se.");

        if (uri.Host == "cdn.pluralkit.me") return input;

        if (_config.ApiInternalUrl == null || _config.AvatarServiceUrl == null)
            return input;

        var kind = type switch
        {
            RehostedImageType.Avatar => "avatar",
            RehostedImageType.Banner => "banner",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        if (!AvatarUtils.IsDiscordCdnUrl(input.Url))
        {
            // just verify image, not rehost
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

            return input;
        }

        if (ctx.Premium)
            kind = $"premium_{kind}";

        try
        {
            var response = await RequestApi("/v2/systems/@me/images", ctx.System.Id, new
            {
                system_uuid = ctx.System.Uuid.ToString(),
                url = input.Url,
                kind,
                uploaded_by = ctx.Author.Id,
            });

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new PKError($"Error uploading image to CDN: {errorText ?? "unknown error"}");
            }

            var success = await response.Content.ReadFromJsonAsync<SuccessResponse>();
            return new ParsedImage { Url = success.Url, Source = AvatarSource.HostedCdn };
        }
        catch (TaskCanceledException e)
        {
            // don't show an internal error to users
            if (e.Message.Contains("HttpClient.Timeout"))
                throw new PKError("Temporary error setting image, please try again later");
            throw;
        }
    }

    public record ErrorResponse(string Error);

    public record SuccessResponse(string Url, bool New);

    public enum RehostedImageType
    {
        Avatar,
        Banner,
    }
}