using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using App.Metrics;

using Autofac;

using Serilog;
using Serilog.Context;

namespace PluralKit.Bot
{
    public class DiscordRequestObserver: IObserver<KeyValuePair<string, object>>
    {
        private readonly IMetrics _metrics;
        private readonly ILogger _logger;

        private bool ShouldLogHeader(string name) =>
            name.StartsWith("x-ratelimit");

        public DiscordRequestObserver(ILogger logger, IMetrics metrics)
        {
            _metrics = metrics;
            _logger = logger.ForContext<DiscordRequestObserver>();
        }
        
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        private string NormalizeRoutePath(string url)
        {
            url = Regex.Replace(url, @"/channels/\d+", "/channels/{channel_id}");
            url = Regex.Replace(url, @"/messages/\d+", "/messages/{message_id}");
            url = Regex.Replace(url, @"/members/\d+", "/members/{user_id}");
            url = Regex.Replace(url, @"/webhooks/\d+/[^/]+", "/webhooks/{webhook_id}/{webhook_token}");
            url = Regex.Replace(url, @"/webhooks/\d+", "/webhooks/{webhook_id}");
            url = Regex.Replace(url, @"/users/\d+", "/users/{user_id}");
            url = Regex.Replace(url, @"/bans/\d+", "/bans/{user_id}");
            url = Regex.Replace(url, @"/roles/\d+", "/roles/{role_id}");
            url = Regex.Replace(url, @"/pins/\d+", "/pins/{message_id}");
            url = Regex.Replace(url, @"/emojis/\d+", "/emojis/{emoji_id}");
            url = Regex.Replace(url, @"/guilds/\d+", "/guilds/{guild_id}");
            url = Regex.Replace(url, @"/integrations/\d+", "/integrations/{integration_id}");
            url = Regex.Replace(url, @"/permissions/\d+", "/permissions/{overwrite_id}");
            url = Regex.Replace(url, @"/reactions/[^{/]+/\d+", "/reactions/{emoji}/{user_id}");
            url = Regex.Replace(url, @"/reactions/[^{/]+", "/reactions/{emoji}");
            url = Regex.Replace(url, @"/invites/[^{/]+", "/invites/{invite_code}");
            
            // catch-all for missed IDs
            url = Regex.Replace(url, @"\d{17,19}", "{snowflake}");
            
            return url;
        }

        private string GetEndpointName(HttpRequestMessage req)
        {
            var localPath = Regex.Replace(req.RequestUri.LocalPath, @"/api/v\d+", "");
            var routePath = NormalizeRoutePath(localPath);
            return $"{req.Method} {routePath}";
        }
        
        private void HandleException(Exception exc, HttpRequestMessage req)
        {
            _logger
                .ForContext("RequestUrlRoute", GetEndpointName(req))
                .Error(exc, "HTTP error: {RequestMethod} {RequestUrl}", req.Method, req.RequestUri);
        }

        private async Task HandleResponse(HttpResponseMessage response, Activity activity)
        {
            var endpoint = GetEndpointName(response.RequestMessage);

            using (LogContext.PushProperty("Elastic", "yes?"))
            {
                if ((int) response.StatusCode >= 400)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    LogContext.PushProperty("ResponseBody", content);
                }

                var headers = response.Headers
                    .Where(header => ShouldLogHeader(header.Key.ToLowerInvariant()))
                    .ToDictionary(k => k.Key.ToLowerInvariant(),
                        v => string.Join(';', v.Value));
                
                _logger
                    .ForContext("RequestUrlRoute", endpoint)
                    .ForContext("ResponseHeaders", headers)
                    .Debug(
                    "HTTP: {RequestMethod} {RequestUrl} -> {ResponseStatusCode} {ResponseStatusString} (in {RequestDurationMs:F1} ms)",
                    response.RequestMessage.Method,
                    response.RequestMessage.RequestUri,
                    (int) response.StatusCode,
                    response.ReasonPhrase,
                    activity.Duration.TotalMilliseconds);
            }

            if (IsDiscordApiRequest(response))
            {
                var timer = _metrics.Provider.Timer.Instance(BotMetrics.DiscordApiRequests, new MetricTags(
                    new[] {"endpoint", "status_code"},
                    new[] {endpoint, ((int) response.StatusCode).ToString()}
                ));
                timer.Record(activity.Duration.Ticks / 10, TimeUnit.Microseconds);
            }
        }

        private static bool IsDiscordApiRequest(HttpResponseMessage response)
        {
            // Assume any properly authorized request is coming from D#+ and not some sort of user
            var authHeader = response.RequestMessage.Headers.Authorization;
            if (authHeader == null || authHeader.Scheme != "Bot")
                return false;

            return response.RequestMessage.RequestUri.AbsoluteUri.StartsWith("https://discord.com/api/");
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            switch (value.Key)
            {
                case "System.Net.Http.HttpRequestOut.Stop":
                {
                    var data = Unsafe.As<ActivityStopData>(value.Value);
                    if (data.Response != null)
                    {
                        var _ = HandleResponse(data.Response, Activity.Current);
                    }

                    break;
                }
                case "System.Net.Http.Exception":
                {
                    var data = Unsafe.As<ExceptionData>(value.Value);
                    HandleException(data.Exception, data.Request);
                    break;
                }
            }
        }

        public static void Install(IComponentContext services)
        {
            DiagnosticListener.AllListeners.Subscribe(new ListenerObserver(services));
        }
        
#pragma warning disable 649
        private class ActivityStopData
        {
            // Field order here matters!
            public HttpResponseMessage Response;
            public HttpRequestMessage Request;
            public TaskStatus RequestTaskStatus;
        }
        
        private class ExceptionData
        {
            // Field order here matters!
            public Exception Exception;
            public HttpRequestMessage Request;
        }
#pragma warning restore 649

        public class ListenerObserver: IObserver<DiagnosticListener>
        {
            private readonly IComponentContext _services;
            private DiscordRequestObserver _observer;

            public ListenerObserver(IComponentContext services)
            {
                _services = services;
            }

            public void OnCompleted() { }

            public void OnError(Exception error) { }

            public void OnNext(DiagnosticListener value)
            {
                if (value.Name != "HttpHandlerDiagnosticListener")
                    return;

                _observer ??= _services.Resolve<DiscordRequestObserver>();
                value.Subscribe(_observer);
            }
        }
    }
}