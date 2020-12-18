using System;

namespace PluralKit.API.Models
{
    public class ApiGroupMembersPatch
    {
        public Guid[] Add { get; set; } = Array.Empty<Guid>();
        public Guid[] Remove { get; set; } = Array.Empty<Guid>();
    }
}