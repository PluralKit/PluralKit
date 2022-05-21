using Myriad.Types;

namespace PluralKit.Bot.Interactive;

public class Button
{
    public string? Label { get; set; }
    public ButtonStyle Style { get; set; } = ButtonStyle.Secondary;
    public string? CustomId { get; set; }
    public bool Disabled { get; set; }
    public Func<InteractionContext, Task> Handler { get; init; }

    public MessageComponent ToMessageComponent() => new()
    {
        Type = ComponentType.Button,
        Label = Label,
        Style = Style,
        CustomId = CustomId,
        Disabled = Disabled
    };
}