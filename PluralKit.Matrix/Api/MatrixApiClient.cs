using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Serilog;

namespace PluralKit.Matrix;

public class MatrixApiClient
{
    private const int MaxAvatarBytes = 10 * 1024 * 1024; // 10 MB

    private readonly HttpClient _client;
    private readonly MatrixConfig _config;
    private readonly ILogger _logger;

    public MatrixApiClient(HttpClient client, MatrixConfig config, ILogger logger)
    {
        _client = client;
        _config = config;
        _logger = logger.ForContext<MatrixApiClient>();
    }

    private string BaseUrl => _config.HomeserverUrl.TrimEnd('/');

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? impersonateUserId = null)
    {
        var url = $"{BaseUrl}{path}";
        if (impersonateUserId != null)
        {
            var separator = url.Contains('?') ? '&' : '?';
            url += $"{separator}user_id={Uri.EscapeDataString(impersonateUserId)}";
        }

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.AsToken);
        return request;
    }

    private async Task<JObject> SendJsonRequest(HttpMethod method, string path, object? body = null, string? impersonateUserId = null)
    {
        var request = CreateRequest(method, path, impersonateUserId);

        if (body != null)
        {
            request.Content = new StringContent(
                JsonConvert.SerializeObject(body),
                Encoding.UTF8,
                "application/json");
        }

        var response = await _client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.Warning("Matrix API error: {StatusCode} {Method} {Path} (user: {UserId}): {Body}",
                (int)response.StatusCode, method.Method, path, impersonateUserId ?? "bot", responseBody);
        }

        response.EnsureSuccessStatusCode();

        try
        {
            return JObject.Parse(responseBody);
        }
        catch (JsonReaderException ex)
        {
            _logger.Error(ex, "Malformed JSON response from {Method} {Path}: {Body}", method.Method, path, responseBody);
            throw;
        }
    }

    /// <summary>Register a virtual user with the homeserver.</summary>
    public async Task RegisterUser(string localpart)
    {
        try
        {
            await SendJsonRequest(HttpMethod.Post, "/_matrix/client/v3/register", new
            {
                type = "m.login.application_service",
                username = localpart,
            });
            _logger.Information("Registered virtual user @{Localpart}:{Server}", localpart, _config.ServerName);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            // M_USER_IN_USE — user already registered, that's fine
            _logger.Debug("Virtual user @{Localpart}:{Server} already registered", localpart, _config.ServerName);
        }
    }

    /// <summary>Join a room as a virtual user.</summary>
    public async Task JoinRoom(string roomId, string virtualMxid)
    {
        await SendJsonRequest(HttpMethod.Post,
            $"/_matrix/client/v3/join/{Uri.EscapeDataString(roomId)}",
            new { },
            impersonateUserId: virtualMxid);
        _logger.Debug("Virtual user {Mxid} joined room {RoomId}", virtualMxid, roomId);
    }

    /// <summary>Send a message as a virtual user. Returns the event ID.</summary>
    public async Task<string> SendMessage(string roomId, string virtualMxid, string body, string? formattedBody, string txnId)
    {
        var content = new JObject
        {
            ["msgtype"] = "m.text",
            ["body"] = body,
        };
        if (formattedBody != null)
        {
            content["format"] = "org.matrix.custom.html";
            content["formatted_body"] = formattedBody;
        }

        var result = await SendJsonRequest(HttpMethod.Put,
            $"/_matrix/client/v3/rooms/{Uri.EscapeDataString(roomId)}/send/m.room.message/{Uri.EscapeDataString(txnId)}",
            content,
            impersonateUserId: virtualMxid);

        return result["event_id"]?.Value<string>()
            ?? throw new InvalidOperationException($"SendMessage response missing event_id for room {roomId}");
    }

    /// <summary>Send an edit as a virtual user. Returns the event ID.</summary>
    public async Task<string> SendEdit(string roomId, string virtualMxid, string originalEventId,
        string body, string? formattedBody, string txnId)
    {
        var newContent = new JObject
        {
            ["msgtype"] = "m.text",
            ["body"] = body,
        };
        if (formattedBody != null)
        {
            newContent["format"] = "org.matrix.custom.html";
            newContent["formatted_body"] = formattedBody;
        }

        var content = new JObject
        {
            ["msgtype"] = "m.text",
            ["body"] = $"* {body}",
            ["m.new_content"] = newContent,
            ["m.relates_to"] = new JObject
            {
                ["rel_type"] = "m.replace",
                ["event_id"] = originalEventId,
            }
        };
        if (formattedBody != null)
        {
            content["format"] = "org.matrix.custom.html";
            content["formatted_body"] = $"* {formattedBody}";
        }

        var result = await SendJsonRequest(HttpMethod.Put,
            $"/_matrix/client/v3/rooms/{Uri.EscapeDataString(roomId)}/send/m.room.message/{Uri.EscapeDataString(txnId)}",
            content,
            impersonateUserId: virtualMxid);

        return result["event_id"]?.Value<string>()
            ?? throw new InvalidOperationException($"SendEdit response missing event_id for room {roomId}");
    }

    /// <summary>Redact an event. Returns true if successful, false if permission denied.</summary>
    public async Task<bool> RedactEvent(string roomId, string eventId, string? reason, string txnId)
    {
        try
        {
            var body = reason != null ? (object)new { reason } : new { };
            await SendJsonRequest(HttpMethod.Put,
                $"/_matrix/client/v3/rooms/{Uri.EscapeDataString(roomId)}/redact/{Uri.EscapeDataString(eventId)}/{Uri.EscapeDataString(txnId)}",
                body);
            return true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.Warning("No permission to redact event {EventId} in room {RoomId}", eventId, roomId);
            return false;
        }
    }

    /// <summary>Set display name for a virtual user.</summary>
    public async Task SetDisplayName(string mxid, string displayName)
    {
        await SendJsonRequest(HttpMethod.Put,
            $"/_matrix/client/v3/profile/{Uri.EscapeDataString(mxid)}/displayname",
            new { displayname = displayName },
            impersonateUserId: mxid);
    }

    /// <summary>Set avatar URL for a virtual user.</summary>
    public async Task SetAvatarUrl(string mxid, string avatarMxcUrl)
    {
        await SendJsonRequest(HttpMethod.Put,
            $"/_matrix/client/v3/profile/{Uri.EscapeDataString(mxid)}/avatar_url",
            new { avatar_url = avatarMxcUrl },
            impersonateUserId: mxid);
    }

    /// <summary>Upload media and get an mxc:// URL.</summary>
    public async Task<string> UploadMedia(byte[] data, string contentType, string filename)
    {
        var request = CreateRequest(HttpMethod.Post,
            $"/_matrix/media/v3/upload?filename={Uri.EscapeDataString(filename)}");
        request.Content = new ByteArrayContent(data);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var response = await _client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            _logger.Warning("Media upload failed: {StatusCode}: {Body}", (int)response.StatusCode, responseBody);

        response.EnsureSuccessStatusCode();

        var result = JObject.Parse(responseBody);
        return result["content_uri"]?.Value<string>()
            ?? throw new InvalidOperationException("Upload response missing content_uri");
    }

    /// <summary>Download media from a URL (for re-uploading avatars). Validates URL and enforces size limit.</summary>
    public async Task<(byte[] data, string contentType)> DownloadMedia(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid media URL: {url}");

        if (uri.Scheme != "https" && uri.Scheme != "http")
            throw new ArgumentException($"Unsupported URL scheme: {uri.Scheme}");

        if (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1"
            || uri.Host.StartsWith("10.") || uri.Host.StartsWith("172.") || uri.Host.StartsWith("192.168.")
            || uri.Host == "169.254.169.254")
            throw new ArgumentException($"Blocked internal URL: {url}");

        var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > MaxAvatarBytes)
            throw new InvalidOperationException($"Media too large: {contentLength} bytes (max {MaxAvatarBytes})");

        var data = await response.Content.ReadAsByteArrayAsync();
        if (data.Length > MaxAvatarBytes)
            throw new InvalidOperationException($"Media too large: {data.Length} bytes (max {MaxAvatarBytes})");

        var ct = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return (data, ct);
    }
}
