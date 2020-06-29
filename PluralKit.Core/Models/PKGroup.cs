using NodaTime;

#nullable enable
namespace PluralKit.Core
{
    public class PKGroup
    {
        public GroupId Id { get; }
        public string Hid { get; } = null!;
        public SystemId System { get; }

        public string Name { get; } = null!;
        public string? Description { get; }
        
        public Instant Created { get; }
    }
}