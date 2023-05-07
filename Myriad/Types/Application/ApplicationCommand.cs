namespace Myriad.Types;

public record ApplicationCommand
{
    public enum ApplicationCommandType
    {
        ChatInput = 1,
        User = 2,
        Message = 3,
    }

    public ulong Id { get; init; }
    public ulong ApplicationId { get; init; }
    public ApplicationCommandType Type { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }
    public ApplicationCommandOption[]? Options { get; init; }
}