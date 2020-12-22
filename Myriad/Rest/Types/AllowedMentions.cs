using System.Collections.Generic;

namespace Myriad.Rest.Types
{
    public record AllowedMentions
    {
        public enum ParseType
        {
            Roles,
            Users,
            Everyone
        }

        public List<ParseType>? Parse { get; set; }
        public List<ulong>? Users { get; set; }
        public List<ulong>? Roles { get; set; }
        public bool RepliedUser { get; set; }
    }
}