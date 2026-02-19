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
    private const int MaxMediaBytes = 50 * 1024 * 1024; // 50 MB

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

    /// <summary>Join a room as a virtual user. Returns false if the join was rejected.</summary>
    public async Task<bool> JoinRoom(string roomId, string virtualMxid)
    {
        try
        {
            await SendJsonRequest(HttpMethod.Post,
                $"/_matrix/client/v3/join/{Uri.EscapeDataString(roomId)}",
                new { },
                impersonateUserId: virtualMxid);
            _logger.Debug("Virtual user {Mxid} joined room {RoomId}", virtualMxid, roomId);
            return true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden
            or HttpStatusCode.NotFound or HttpStatusCode.TooManyRequests)
        {
            _logger.Warning("Virtual user {Mxid} cannot join {Room}: {Status}", virtualMxid, roomId, ex.StatusCode);
            return false;
        }
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

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        int totalRead = 0, bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > MaxAvatarBytes)
                throw new InvalidOperationException($"Media too large: exceeded {MaxAvatarBytes} bytes");
            ms.Write(buffer, 0, bytesRead);
        }
        var data = ms.ToArray();

        var ct = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return (data, ct);
    }

    /// <summary>Download media from an mxc:// URL via the homeserver's media API.</summary>
    public async Task<(byte[] data, string contentType)> DownloadMxcMedia(string mxcUrl)
    {
        // Parse mxc://server/mediaId
        if (!mxcUrl.StartsWith("mxc://"))
            throw new ArgumentException($"Invalid mxc URL: {mxcUrl}");

        var parts = mxcUrl.Substring("mxc://".Length).Split('/', 2);
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid mxc URL format: {mxcUrl}");

        var server = parts[0];
        var mediaId = parts[1];

        var request = CreateRequest(HttpMethod.Get,
            $"/_matrix/media/v3/download/{Uri.EscapeDataString(server)}/{Uri.EscapeDataString(mediaId)}");

        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > MaxMediaBytes)
            throw new InvalidOperationException($"Media too large: {contentLength} bytes (max {MaxMediaBytes})");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        int totalRead = 0, bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > MaxMediaBytes)
                throw new InvalidOperationException($"Media too large: exceeded {MaxMediaBytes} bytes");
            ms.Write(buffer, 0, bytesRead);
        }

        var ct = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return (ms.ToArray(), ct);
    }

    /// <summary>Send a media message (m.image, m.file, m.video, m.audio) as a virtual user. Returns the event ID.</summary>
    public async Task<string> SendMediaMessage(string roomId, string virtualMxid, string msgtype,
        string mxcUrl, string body, JObject? info, string txnId)
    {
        var content = new JObject
        {
            ["msgtype"] = msgtype,
            ["url"] = mxcUrl,
            ["body"] = body,
        };
        if (info != null)
            content["info"] = info;

        var result = await SendJsonRequest(HttpMethod.Put,
            $"/_matrix/client/v3/rooms/{Uri.EscapeDataString(roomId)}/send/m.room.message/{Uri.EscapeDataString(txnId)}",
            content,
            impersonateUserId: virtualMxid);

        return result["event_id"]?.Value<string>()
            ?? throw new InvalidOperationException($"SendMediaMessage response missing event_id for room {roomId}");
    }

    /// <summary>Set room-level display name for a virtual user via member state event.</summary>
    public async Task SetRoomDisplayName(string roomId, string virtualMxid, string displayName)
    {
        await SendJsonRequest(HttpMethod.Put,
            $"/_matrix/client/v3/rooms/{Uri.EscapeDataString(roomId)}/state/m.room.member/{Uri.EscapeDataString(virtualMxid)}",
            new { membership = "join", displayname = displayName },
            impersonateUserId: virtualMxid);
    }

    /// <summary>Get a user's power level in a room. Returns -1 if unable to determine.</summary>
    public async Task<int> GetUserPowerLevel(string roomId, string userId)
    {
        try
        {
            var result = await SendJsonRequest(HttpMethod.Get,
                $"/_matrix/client/v3/rooms/{Uri.EscapeDataString(roomId)}/state/m.room.power_levels");

            var users = result["users"] as JObject;
            if (users?[userId] != null)
                return users[userId]!.Value<int>();

            return result["users_default"]?.Value<int>() ?? 0;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to get power levels for room {RoomId}", roomId);
            return -1;
        }
    }
}
