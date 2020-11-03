using System.Collections.Generic;

namespace PluralKit.API.Models
{
    public class ApiSwitchList
    {
        public IEnumerable<ApiMember> Members { get; set; }
        public IEnumerable<ApiSwitch> Switches { get; set; }
    }
}