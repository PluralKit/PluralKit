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
            url = Regex.Replace(url, @"/channels/\d{17,19}", "/channels/{channel_id}");
            url = Regex.Replace(url, @"/messages/\d{17,19}", "/messages/{message_id}");
            url = Regex.Replace(url, @"/members/\d{17,19}", "/members/{user_id}");
            url = Regex.Replace(url, @"/webhooks/\d{17,19}/[^/]+", "/webhooks/{webhook_id}/{webhook_token}");
            url = Regex.Replace(url, @"/webhooks/\d{17,19}", "/webhooks/{webhook_id}");
            url = Regex.Replace(url, @"/users/\d{17,19}", "/users/{user_id}");
            url = Regex.Replace(url, @"/bans/\d{17,19}", "/bans/{user_id}");
            url = Regex.Replace(url, @"/roles/\d{17,19}", "/roles/{role_id}");
            url = Regex.Replace(url, @"/pins/\d{17,19}", "/pins/{message_id}");
            url = Regex.Replace(url, @"/emojis/\d{17,19}", "/emojis/{emoji_id}");
            url = Regex.Replace(url, @"/guilds/\d{17,19}", "/guilds/{guild_id}");
            url = Regex.Replace(url, @"/integrations/\d{17,19}", "/integrations/{integration_id}");
            url = Regex.Replace(url, @"/permissions/\d{17,19}", "/permissions/{overwrite_id}");
            url = Regex.Replace(url, @"/reactions/[^{/]+/\d{17,19}", "/reactions/{emoji}/{user_id}");
            url = Regex.Replace(url, @"/reactions/[^{/]+", "/reactions/{emoji}");
            url = Regex.Replace(url, @"/invites/[^{/]+", "/invites/{invite_code}");
            return url;
        }

        private string Endpoint(HttpRequestMessage req)
        {
            var routePath = NormalizeRoutePath(req.RequestUri.LocalPath.Replace("/api/v7", ""));
            return $"{req.Method} {routePath}";
        }
        
        private void HandleException(Exception exc, HttpRequestMessage req)
        {
            _logger
                .ForContext("RequestUrlRoute", Endpoint(req))
                .Error(exc, "HTTP error: {RequestMethod} {RequestUrl}", req.Method, req.RequestUri);
        }

        private async Task HandleResponse(HttpResponseMessage response, Activity activity)
        {
            if (response.RequestMessage.RequestUri.Host != "discord.com")
                return;
            
            var endpoint = Endpoint(response.RequestMessage);

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

            var timer = _metrics.Provider.Timer.Instance(BotMetrics.DiscordApiRequests, new MetricTags(
                new[] {"endpoint", "status_code"},
                new[] {endpoint, ((int) response.StatusCode).ToString()}
            ));
            timer.Record(activity.Duration.Ticks / 10, TimeUnit.Microseconds);
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