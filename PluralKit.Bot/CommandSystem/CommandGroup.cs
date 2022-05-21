namespace PluralKit.Bot;

public class CommandGroup
{
    public CommandGroup(string key, string description, ICollection<Command> children)
    {
        Key = key;
        Description = description;
        Children = children;
    }

    public string Key { get; }
    public string Description { get; }

    public ICollection<Command> Children { get; }
}