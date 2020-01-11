using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

namespace PluralKit.API.Controllers
{
    public struct MessageReturn
    {
        [JsonProperty("timestamp")] public Instant Timestamp;
        [JsonProperty("id")] public string Id;
        [JsonProperty("original")] public string Original;
        [JsonProperty("sender")] public string Sender;
        [JsonProperty("channel")] public string Channel;

        [JsonProperty("system")] public JObject System;
        [JsonProperty("member")] public JObject Member;
    }
    
    [ApiController]
    [Route("v1/msg")]
    [Route("msg")]
    public class MessageController: ControllerBase
    {
        private IDataStore _data;
        private TokenAuthService _auth;

        public MessageController(IDataStore _data, TokenAuthService auth)
        {
            this._data = _data;
            _auth = auth;
        }

        [HttpGet("{mid}")]
        public async Task<ActionResult<MessageReturn>> GetMessage(ulong mid)
        {
            var msg = await _data.GetMessage(mid);
            if (msg == null) return NotFound("Message not found.");

            return new MessageReturn
            {
                Timestamp = Instant.FromUnixTimeMilliseconds((long) (msg.Message.Mid >> 22) + 1420070400000),
                Id = msg.Message.Mid.ToString(),
                Channel = msg.Message.Channel.ToString(),
                Sender = msg.Message.Sender.ToString(),
                Member = msg.Member.ToJson(_auth.ContextFor(msg.System)),
                System = msg.System.ToJson(_auth.ContextFor(msg.System)),
                Original = msg.Message.OriginalMid?.ToString()
            };
        } 
    }
}