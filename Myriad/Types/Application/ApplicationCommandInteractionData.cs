namespace Myriad.Types
{
    public record ApplicationCommandInteractionData
    {
        public ulong Id { get; init; }
        public string Name { get; init; }
        public ApplicationCommandInteractionDataOption[] Options { get; init; }
    }
}