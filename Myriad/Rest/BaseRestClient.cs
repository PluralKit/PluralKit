using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

using Myriad.Rest.Exceptions;
using Myriad.Rest.Ratelimit;
using Myriad.Rest.Types;
using Myriad.Serialization;

using Polly;

using Serilog;
using Serilog.Context;

namespace Myriad.Rest
{
    public class BaseRestClient: IAsyncDisposable
    {
        private const string ApiBaseUrl = "https://discord.com/api/v9";

        private readonly Version _httpVersion = new(2, 0);
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly ILogger _logger;
        private readonly Ratelimiter _ratelimiter;
        private readonly AsyncPolicy<HttpResponseMessage> _retryPolicy;

        public BaseRestClient(string userAgent, string token, ILogger logger)
        {
            _logger = logger.ForContext<BaseRestClient>();

            if (!token.StartsWith("Bot "))
                token = "Bot " + token;

            Client = new HttpClient();
            Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
            Client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);

            _jsonSerializerOptions = new JsonSerializerOptions().ConfigureForMyriad();

            _ratelimiter = new Ratelimiter(logger);
            var discordPolicy = new DiscordRateLimitPolicy(_ratelimiter);

            // todo: why doesn't the timeout work? o.o
            var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10));

            var waitPolicy = Policy
                .Handle<RatelimitBucketExhaustedException>()
                .WaitAndRetryAsync(3,
                    (_, e, _) => ((RatelimitBucketExhaustedException)e).RetryAfter,
                    (_, _, _, _) => Task.CompletedTask)
                .AsAsyncPolicy<HttpResponseMessage>();

            _retryPolicy = Policy.WrapAsync(timeoutPolicy, waitPolicy, discordPolicy);
        }

        public HttpClient Client { get; }

        public ValueTask DisposeAsync()
        {
            _ratelimiter.Dispose();
            Client.Dispose();
            return default;
        }

        public async Task<T?> Get<T>(string path, (string endpointName, ulong major) ratelimitParams) where T : class
        {
            using var response = await Send(() => new HttpRequestMessage(HttpMethod.Get, ApiBaseUrl + path),
                ratelimitParams, true);

            // GET-only special case: 404s are nulls and not exceptions
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            return await ReadResponse<T>(response);
        }

        public async Task<T?> Post<T>(string path, (string endpointName, ulong major) ratelimitParams, object? body)
            where T : class
        {
            using var response = await Send(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + path);
                SetRequestJsonBody(request, body);
                return request;
            }, ratelimitParams);
            return await ReadResponse<T>(response);
        }

        public async Task<T?> PostMultipart<T>(string path, (string endpointName, ulong major) ratelimitParams, object? payload, MultipartFile[]? files)
            where T : class
        {
            using var response = await Send(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + path);
                SetRequestFormDataBody(request, payload, files);
                return request;
            }, ratelimitParams);
            return await ReadResponse<T>(response);
        }

        public async Task<T?> Patch<T>(string path, (string endpointName, ulong major) ratelimitParams, object? body)
            where T : class
        {
            using var response = await Send(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Patch, ApiBaseUrl + path);
                SetRequestJsonBody(request, body);
                return request;
            }, ratelimitParams);
            return await ReadResponse<T>(response);
        }

        public async Task<T?> Put<T>(string path, (string endpointName, ulong major) ratelimitParams, object? body)
            where T : class
        {
            using var response = await Send(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Put, ApiBaseUrl + path);
                SetRequestJsonBody(request, body);
                return request;
            }, ratelimitParams);
            return await ReadResponse<T>(response);
        }

        public async Task Delete(string path, (string endpointName, ulong major) ratelimitParams)
        {
            using var _ = await Send(() => new HttpRequestMessage(HttpMethod.Delete, ApiBaseUrl + path), ratelimitParams);
        }

        private void SetRequestJsonBody(HttpRequestMessage request, object? body)
        {
            if (body == null) return;
            request.Content =
                new ReadOnlyMemoryContent(JsonSerializer.SerializeToUtf8Bytes(body, _jsonSerializerOptions));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        private void SetRequestFormDataBody(HttpRequestMessage request, object? payload, MultipartFile[]? files)
        {
            var bodyJson = JsonSerializer.SerializeToUtf8Bytes(payload, _jsonSerializerOptions);

            var mfd = new MultipartFormDataContent();
            mfd.Add(new ByteArrayContent(bodyJson), "payload_json");

            if (files != null)
            {
                for (var i = 0; i < files.Length; i++)
                {
                    var (filename, stream) = files[i];
                    mfd.Add(new StreamContent(stream), $"file{i}", filename);
                }
            }

            request.Content = mfd;
        }

        private async Task<T?> ReadResponse<T>(HttpResponseMessage response) where T : class
        {
            if (response.StatusCode == HttpStatusCode.NoContent)
                return null;
            return await response.Content.ReadFromJsonAsync<T>(_jsonSerializerOptions);
        }

        private async Task<HttpResponseMessage> Send(Func<HttpRequestMessage> createRequest,
                                                     (string endpointName, ulong major) ratelimitParams,
                                                     bool ignoreNotFound = false)
        {
            return await _retryPolicy.ExecuteAsync(async _ =>
                {
                    using var __ = LogContext.PushProperty("EndpointName", ratelimitParams.endpointName);

                    var request = createRequest();
                    _logger.Debug("Request: {RequestMethod} {RequestPath}",
                        request.Method, request.RequestUri);

                    request.Version = _httpVersion;
                    request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    stopwatch.Stop();

                    _logger.Debug(
                        "Response: {RequestMethod} {RequestPath} -> {StatusCode} {ReasonPhrase} (in {ResponseDurationMs} ms)",
                        request.Method, request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, stopwatch.ElapsedMilliseconds);

                    await HandleApiError(response, ignoreNotFound);

                    return response;
                },
                new Dictionary<string, object>
                {
                    {DiscordRateLimitPolicy.EndpointContextKey, ratelimitParams.endpointName},
                    {DiscordRateLimitPolicy.MajorContextKey, ratelimitParams.major}
                });
        }

        private async ValueTask HandleApiError(HttpResponseMessage response, bool ignoreNotFound)
        {
            if (response.IsSuccessStatusCode)
                return;

            if (response.StatusCode == HttpStatusCode.NotFound && ignoreNotFound)
                return;

            var body = await response.Content.ReadAsStringAsync();
            var apiError = TryParseApiError(body);
            if (apiError != null)
                _logger.Warning("Discord API error: {DiscordErrorCode} {DiscordErrorMessage}", apiError.Code, apiError.Message);

            throw CreateDiscordException(response, body, apiError);
        }

        private DiscordRequestException CreateDiscordException(HttpResponseMessage response, string body, DiscordApiError? apiError)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.BadRequest => new BadRequestException(response, body, apiError),
                HttpStatusCode.Forbidden => new ForbiddenException(response, body, apiError),
                HttpStatusCode.Unauthorized => new UnauthorizedException(response, body, apiError),
                HttpStatusCode.NotFound => new NotFoundException(response, body, apiError),
                HttpStatusCode.Conflict => new ConflictException(response, body, apiError),
                HttpStatusCode.TooManyRequests => new TooManyRequestsException(response, body, apiError),
                _ => new UnknownDiscordRequestException(response, body, apiError)
            };
        }

        private DiscordApiError? TryParseApiError(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return null;

            try
            {
                return JsonSerializer.Deserialize<DiscordApiError>(responseBody, _jsonSerializerOptions);
            }
            catch (JsonException e)
            {
                _logger.Verbose(e, "Error deserializing API error");
            }

            return null;
        }
    }
}