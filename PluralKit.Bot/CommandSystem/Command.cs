using Newtonsoft.Json;

namespace PluralKit.Bot
{
    public class Command
    {
        [JsonProperty("key")] public string Key { get; }
        [JsonProperty("usage")] public string Usage { get; }
        [JsonProperty("description")] public string Description { get; }
        
        public Command(string key, string usage, string description)
        {
            Key = key;
            Usage = usage;
            Description = description;
        }
    }
}