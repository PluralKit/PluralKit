namespace Myriad.Types
{
    public record MessageComponent
    {
        public ComponentType Type { get; init; }
        public ButtonStyle? Style { get; init; }
        public string? Label { get; init; }
        public Emoji? Emoji { get; init; }
        public string? CustomId { get; init; }
        public string? Url { get; init; }
        public bool? Disabled { get; init; }
        public MessageComponent[]? Components { get; init; }
        
        public enum ComponentType
        {
            ActionRow = 1,
            Button = 2
        }

        public enum ButtonStyle
        {
            Primary = 1,
            Secondary = 2,
            Success = 3,
            Danger = 4,
            Link = 5
        }
    }
}