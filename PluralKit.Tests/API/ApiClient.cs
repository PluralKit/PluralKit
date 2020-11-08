#nullable enable
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.JsonNet;

using PluralKit.Core;

namespace PluralKit.Tests.API
{
    public class ApiClient
    {
        private readonly HttpClient _client;
        private readonly JsonSerializerSettings _settings;

        public ApiClient(HttpClient client)
        {
            _client = client;
            _settings = new JsonSerializerSettings
            {
                ContractResolver = new PartialContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            }.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        }

        public async Task<T> Send<T>(HttpMethod method, string url, string? token = null, object? body = null)
        {
            var resp = await SendRaw(method, url, token);
            return await Parse<T>(resp);
        }

        public async Task<T> Parse<T>(HttpResponseMessage resp)
        {
            var body = await resp.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(body, _settings)!;
        }

        public async Task<HttpResponseMessage> SendRaw(HttpRequestMessage req) => 
            await _client.SendAsync(req);

        public async Task<HttpResponseMessage> SendRaw(HttpMethod method, string url, string? token = null, object? body = null) => 
            await SendRaw(Prepare(method, url, token, body));

        public HttpRequestMessage Prepare(HttpMethod method, string url, string? token = null, object? body = null)
        {
            var req = new HttpRequestMessage(method, url);
            if (body != null)
            {
                req.Content = new StringContent(JsonConvert.SerializeObject(body, _settings), Encoding.UTF8, "application/json");
            }

            if (token != null)
                req.Headers.TryAddWithoutValidation("Authorization", token);
            return req;
        }
    }
}