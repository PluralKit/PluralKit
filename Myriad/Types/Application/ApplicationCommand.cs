namespace Myriad.Types
{
    public record ApplicationCommand
    {
        public ulong Id { get; init; }
        public ulong ApplicationId { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public ApplicationCommandOption[]? Options { get; init; }
    }
}