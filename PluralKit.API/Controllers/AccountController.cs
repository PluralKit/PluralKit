using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace PluralKit.API.Controllers
{
    [ApiController]
    [Route("a")]
    [Route("v1/a")]
    public class AccountController: ControllerBase
    {
        private SystemStore _systems;

        public AccountController(SystemStore systems)
        {
            _systems = systems;
        }

        [HttpGet("{aid}")]
        public async Task<ActionResult<PKSystem>> GetSystemByAccount(ulong aid)
        {
            var system = await _systems.GetByAccount(aid);
            if (system == null) return NotFound("Account not found.");
            
            return Ok(system);
        }
    }
}