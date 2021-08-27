namespace Myriad.Types
{
    public record Role
    {
        public ulong Id { get; init; }
        public string Name { get; init; }
        public uint Color { get; init; }
        public bool Hoist { get; init; }
        public int Position { get; init; }
        public PermissionSet Permissions { get; init; }
        public bool Managed { get; init; }
        public bool Mentionable { get; init; }
    }
}