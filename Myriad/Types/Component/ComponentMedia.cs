namespace Myriad.Types;

public record ComponentMedia
{
    public string? Url { get; init; }
}

public record ComponentMediaItem
{
    public ComponentMedia Media { get; init; }
    public string? Description { get; init; }
    public bool Spoiler { get; init; } = false;
}