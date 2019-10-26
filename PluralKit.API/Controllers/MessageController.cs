using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NodaTime;

namespace PluralKit.API.Controllers
{
    public struct MessageReturn
    {
        [JsonProperty("timestamp")] public Instant Timestamp;
        [JsonProperty("id")] public string Id;
        [JsonProperty("sender")] public string Sender;
        [JsonProperty("channel")] public string Channel;

        [JsonProperty("system")] public PKSystem System;
        [JsonProperty("member")] public PKMember Member;
    }
    
    [ApiController]
    [Route("v1/msg")]
    [Route("msg")]
    public class MessageController: ControllerBase
    {
        private IDataStore _data;

        public MessageController(IDataStore _data)
        {
            this._data = _data;
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
                Member = msg.Member,
                System = msg.System
            };
        } 
    }
}