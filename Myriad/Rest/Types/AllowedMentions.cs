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

        public ParseType[]? Parse { get; set; }
        public ulong[]? Users { get; set; }
        public ulong[]? Roles { get; set; }
        public bool RepliedUser { get; set; }
    }
}