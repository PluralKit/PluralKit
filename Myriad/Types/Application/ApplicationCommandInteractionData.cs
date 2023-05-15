namespace Myriad.Types;

public record ApplicationCommandInteractionData
{
    public ulong? Id { get; init; }
    public string? Name { get; init; }
    public ApplicationCommandInteractionDataOption[]? Options { get; init; }
    public string? CustomId { get; init; }
    public ulong? TargetId { get; init; }
    public ComponentType? ComponentType { get; init; }
    public InteractionResolvedData Resolved { get; init; }
    public MessageComponent[]? Components { get; init; }

    public record InteractionResolvedData
    {
        public Dictionary<ulong, Message>? Messages { get; init; }
        public Dictionary<ulong, User>? Users { get; init; }
    }
}