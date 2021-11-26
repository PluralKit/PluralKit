using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/msg")]
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
        public async Task<ActionResult<JObject>> GetMessage(ulong mid)
        {
            var msg = await _db.Execute(c => _repo.GetMessage(c, mid));
            if (msg == null) return NotFound("Message not found.");

            return msg.ToJson(User.ContextFor(msg.System), APIVersion.V1);
        }
    }
}