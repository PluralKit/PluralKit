using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PluralKit.Web.Pages
{
    public class ViewSystem : PageModel
    {
        private IDataStore _data;

        public ViewSystem(IDataStore data)
        {
            _data = data;
        }
        
        public PKSystem System { get; set; }
        public IEnumerable<PKMember> Members { get; set; }

        public async Task<IActionResult> OnGet(string systemId)
        {
            System = await _data.GetSystemByHid(systemId);
            if (System == null) return NotFound();

            Members = await _data.GetSystemMembers(System);

            return Page();
        }
    }
}