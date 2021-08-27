namespace Myriad.Types
{
    public record ApplicationCommandInteractionData
    {
        public ulong? Id { get; init; }
        public string? Name { get; init; }
        public ApplicationCommandInteractionDataOption[]? Options { get; init; }
        public string? CustomId { get; init; }
        public ComponentType? ComponentType { get; init; }
    }
}