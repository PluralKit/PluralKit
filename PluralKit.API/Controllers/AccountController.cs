using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("a")]
    [Route( "v{version:apiVersion}/a" )]
    public class AccountController: ControllerBase
    {
        private IDataStore _data;
        private TokenAuthService _auth;

        public AccountController(IDataStore data, TokenAuthService auth)
        {
            _data = data;
            _auth = auth;
        }

        [HttpGet("{aid}")]
        public async Task<ActionResult<JObject>> GetSystemByAccount(ulong aid)
        {
            var system = await _data.GetSystemByAccount(aid);
            if (system == null) return NotFound("Account not found.");
            
            return Ok(system.ToJson(_auth.ContextFor(system)));
        }
    }
}