using System.Collections.Generic;
using Newtonsoft.Json;

namespace PluralKit.Bot
{
    public class CommandGroup
    {
        [JsonProperty("key")] public string Key { get; }
        [JsonProperty("title")] public string Title { get; }
        [JsonProperty("description")] public string Description { get; }
        [JsonProperty("commands")] public string[] _commands { get; }
        public ICollection<Command> Commands { get; init; }

        public CommandGroup(string key, string title, string description, string[] commands)
        {
            Key = key;
            Title = title;
            Description = description;
            _commands = commands;
            Commands = new List<Command>();
        }

    }
}