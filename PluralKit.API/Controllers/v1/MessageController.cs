using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

[ApiController]
[Route("v1")]
public class MessageController: ControllerBase
{
    private readonly IDatabase _db;
    private readonly ModelRepository _repo;

    public MessageController(ModelRepository repo, IDatabase db)
    {
        _repo = repo;
        _db = db;
    }

    [HttpGet("msg/{mid}")]
    public async Task<ActionResult<JObject>> GetMessage(ulong mid)
    {
        var msg = await _db.Execute(c => _repo.GetMessage(c, mid));
        if (msg == null) return NotFound("Message not found.");

        var ctx = msg.System == null ? LookupContext.ByNonOwner : User.ContextFor(msg.System);
        return msg.ToJson(ctx, APIVersion.V1);
    }
}