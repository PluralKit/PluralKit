using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route( "v{version:apiVersion}/a" )]
    public class AccountController: ControllerBase
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        public AccountController(IDatabase db, ModelRepository repo)
        {
            _db = db;
            _repo = repo;
        }

        [HttpGet("{aid}")]
        public async Task<ActionResult<JObject>> GetSystemByAccount(ulong aid)
        {
            var system = await _db.Execute(c => _repo.GetSystemByAccount(c, aid));
            if (system == null)
                return NotFound("Account not found.");
            
            return Ok(system.ToJson(User.ContextFor(system)));
        }
    }
}