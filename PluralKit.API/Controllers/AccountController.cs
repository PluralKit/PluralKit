using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace PluralKit.API.Controllers
{
    [ApiController]
    [Route("a")]
    [Route("v1/a")]
    public class AccountController: ControllerBase
    {
        private IDataStore _data;

        public AccountController(IDataStore data)
        {
            _data = data;
        }

        [HttpGet("{aid}")]
        public async Task<ActionResult<PKSystem>> GetSystemByAccount(ulong aid)
        {
            var system = await _data.GetSystemByAccount(aid);
            if (system == null) return NotFound("Account not found.");
            
            return Ok(system);
        }
    }
}