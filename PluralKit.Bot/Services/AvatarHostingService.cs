using System.Net;
using System.Net.Http.Json;

namespace PluralKit.Bot;

public class AvatarHostingService
{
    private readonly BotConfig _config;
    private readonly HttpClient _client;

    public AvatarHostingService(BotConfig config, HttpClient client)
    {
        _config = config;
        _client = client;
    }

    public async Task<ParsedImage> TryRehostImage(ParsedImage input, RehostedImageType type, ulong userId)
    {
        var uploaded = await TryUploadAvatar(input.Url, type, userId);
        if (uploaded != null)
        {
            // todo: make new image type called Cdn?
            return new ParsedImage { Url = uploaded, Source = AvatarSource.HostedCdn };
        }

        return input;
    }

    public async Task<string?> TryUploadAvatar(string? avatarUrl, RehostedImageType type, ulong userId)
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
            new { url = avatarUrl, kind, uploaded_by = userId });
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