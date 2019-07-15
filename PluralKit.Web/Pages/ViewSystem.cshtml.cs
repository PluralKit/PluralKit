using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PluralKit.Web.Pages
{
    public class ViewSystem : PageModel
    {
        private SystemStore _systems;
        private MemberStore _members;

        public ViewSystem(SystemStore systems, MemberStore members)
        {
            _systems = systems;
            _members = members;
        }
        
        public PKSystem System { get; set; }
        public IEnumerable<PKMember> Members { get; set; }

        public async Task<IActionResult> OnGet(string systemId)
        {
            System = await _systems.GetByHid(systemId);
            if (System == null) return NotFound();

            Members = await _members.GetBySystem(System);

            return Page();
        }
    }
}