using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public DiscordRequestObserver(ILogger logger, IMetrics metrics)
        {
            _metrics = metrics;
            _logger = logger.ForContext<DiscordRequestObserver>();
        }
        
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public string NormalizeRoutePath(string url)
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

        public async Task HandleResponse(HttpResponseMessage response, Activity activity)
        {
            if (response.RequestMessage.RequestUri.Host != "discord.com")
                return;
            
            using (LogContext.PushProperty("Elastic", "yes?"))
            {
                if ((int) response.StatusCode >= 400 && (int) response.StatusCode < 500)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    LogContext.PushProperty("ResponseBody", content);
                }
                
                var routePath = NormalizeRoutePath(response.RequestMessage.RequestUri.LocalPath.Replace("/api/v7", ""));
                var route = $"{response.RequestMessage.Method} {routePath}";
                LogContext.PushProperty("DiscordRoute", route);

                _logger.Information(
                    "{RequestMethod} {RequestUrl} -> {ResponseStatusCode} {ResponseStatusString} (in {RequestDurationMs:F1} ms)",
                    response.RequestMessage.Method.Method,
                    response.RequestMessage.RequestUri,
                    (int) response.StatusCode,
                    response.ReasonPhrase,
                    activity.Duration.TotalMilliseconds);
            }

            var timer = _metrics.Provider.Timer.Instance(BotMetrics.DiscordApiRequests);
            timer.Record(activity.Duration.Ticks / 10, TimeUnit.Microseconds, ((int) response.StatusCode).ToString());
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key == "System.Net.Http.HttpRequestOut.Stop")
            {
                var data = Unsafe.As<TypedData>(value.Value);
                var _ = HandleResponse(data.Response, Activity.Current);
            }
        }
        
        public static void Install(IComponentContext services)
        {
            DiagnosticListener.AllListeners.Subscribe(new ListenerObserver(services));
        }
        
        private class TypedData
        {
            // Field order here matters!
            public HttpResponseMessage Response;
            public HttpRequestMessage Request;
            public TaskStatus RequestTaskStatus;
        }

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