using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API
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
    [ApiVersion("1.0")]
    [Route( "v{version:apiVersion}/msg" )]
    public class MessageController: ControllerBase
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;

        public MessageController(ModelRepository repo, IDatabase db)
        {
            _repo = repo;
            _db = db;
        }

        [HttpGet("{mid}")]
        public async Task<ActionResult<MessageReturn>> GetMessage(ulong mid)
        {
            var msg = await _db.Execute(c => _repo.GetMessage(c, mid));
            if (msg == null) return NotFound("Message not found.");

            return new MessageReturn
            {
                Timestamp = Instant.FromUnixTimeMilliseconds((long) (msg.Message.Mid >> 22) + 1420070400000),
                Id = msg.Message.Mid.ToString(),
                Channel = msg.Message.Channel.ToString(),
                Sender = msg.Message.Sender.ToString(),
                Member = msg.Member.ToJson(User.ContextFor(msg.System)),
                System = msg.System.ToJson(User.ContextFor(msg.System)),
                Original = msg.Message.OriginalMid?.ToString()
            };
        } 
    }
}