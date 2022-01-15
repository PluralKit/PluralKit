namespace Myriad.Types;

public record Sticker
{
    public enum StickerType
    {
        STANDARD = 1,
        GUILD = 2,
    }

    public enum StickerFormatType
    {
        PNG = 1,
        APNG = 2,
        LOTTIE = 3,
    }

    public ulong Id { get; init; }
    public StickerType Type { get; init; }
    public ulong? PackId { get; init; }
    public string Name { get; init; }
    public string? Description { get; init; }
    public string Tags { get; init; }
    public string Asset { get; init; }
    public bool Available { get; init; }
    public ulong? GuildId { get; init; }
    public User? User { get; init; }
    public int? SortValue { get; init; }
}