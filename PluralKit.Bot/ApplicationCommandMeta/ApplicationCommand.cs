using ApplicationCommandType = Myriad.Types.ApplicationCommand.ApplicationCommandType;

namespace PluralKit.Bot;

public class ApplicationCommand
{
    public ApplicationCommand(ApplicationCommandType type, string name, string? description = null)
    {
        Type = type;
        Name = name;
        Description = description;
    }

    public ApplicationCommandType Type { get; }
    public string Name { get; }
    public string Description { get; }
}