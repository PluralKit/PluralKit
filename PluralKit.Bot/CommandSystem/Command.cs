namespace PluralKit.Bot
{
    public class Command
    {
        public string Key { get; }
        public string Usage { get; }
        public string Description { get; }
        
        public Command(string key, string usage, string description)
        {
            Key = key;
            Usage = usage;
            Description = description;
        }
    }
}