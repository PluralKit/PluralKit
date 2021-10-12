using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("v{version:apiVersion}")]
    public class MetaControllerV2: PKControllerBase
    {
        public MetaControllerV2(IServiceProvider svc) : base(svc) { }

        [HttpGet("meta")]
        public async Task<ActionResult<JObject>> Meta()
        {
            await using var conn = await _db.Obtain();
            var shards = await _repo.GetShards(conn);

            var o = new JObject();
            o.Add("shards", shards.ToJSON());

            return Ok(o);
        }

        [HttpGet("messages/{messageId}")]
        public async Task<ActionResult<MessageReturn>> MessageGet(ulong messageId)
        {
            var msg = await _db.Execute(c => _repo.GetMessage(c, messageId));
            if (msg == null)
                throw APIErrors.MessageNotFound;

            var ctx = this.ContextFor(msg.System);

            // todo: don't rely on v1 stuff
            return new MessageReturn
            {
                Timestamp = Instant.FromUnixTimeMilliseconds((long)(msg.Message.Mid >> 22) + 1420070400000),
                Id = msg.Message.Mid.ToString(),
                Channel = msg.Message.Channel.ToString(),
                Sender = msg.Message.Sender.ToString(),
                System = msg.System.ToJson(ctx, v: APIVersion.V2),
                Member = msg.Member.ToJson(ctx, v: APIVersion.V2),
                Original = msg.Message.OriginalMid?.ToString()
            };
        }
    }
}