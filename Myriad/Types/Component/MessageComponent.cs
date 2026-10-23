namespace Myriad.Types;

public record MessageComponent
{
    public ComponentType Type { get; init; }
    public ButtonStyle? Style { get; set; }
    public string? Label { get; init; }
    public string? Content { get; init; }
    public Emoji? Emoji { get; init; }
    public string? CustomId { get; init; }
    public string? Url { get; init; }
    public bool? Disabled { get; init; }
    public uint? AccentColor { get; init; }
    public int? Spacing { get; init; }
    public ComponentMedia? Media { get; init; }
    public ComponentMediaItem[]? Items { get; init; }

    public MessageComponent? Accessory { get; init; }
    public MessageComponent[]? Components { get; init; }
    public string? Placeholder { get; init; }
    public uint? MinLength { get; init; }
    public uint? MaxLength { get; init; }
    public uint? MinValues { get; init; }
    public uint? MaxValues { get; init; }
    public MessageComponent? Component { get; init; }
    public string? Value { get; init; }
    public string[]? Values { get; init; }
    public bool? Required { get; init; }

    public MessageComponentOption[]? Options { get; init; }

    public record MessageComponentOption
    {
        public string? Label { get; init; }
        public string? Value { get; init; }
        public bool? Default { get; init; }
    }
}