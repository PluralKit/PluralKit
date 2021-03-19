using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PluralKit.Bot
{
    public class CommandReferenceStore
    {

        private readonly Dictionary<string, Command> _commands = new Dictionary<string, Command>();
        private readonly Dictionary<string, CommandGroup> _groups = new Dictionary<string, CommandGroup>();

        public Command GetCommand(string commandKey)
        {
            if (_commands.TryGetValue(commandKey, out var cmd)) return cmd;
            return null;
        }

        public CommandGroup GetGroup(string groupKey)
        {
            if (_groups.TryGetValue(groupKey, out var grp)) return grp;
            return null;
        }

        // a kinda crappy way to init the dictionaries
        public async Task Init()
        {
            string data;

            using (var stream = Assembly.GetEntryAssembly()?.GetManifestResourceStream("commandreference.json"))
            {
                if (stream == null) throw new Exception("Unable to load command documentation");
                using (var reader = new StreamReader(stream)) data = await reader.ReadToEndAsync();
            }

            var j = JsonConvert.DeserializeObject<CommandReferenceFile>(data);

            foreach (Command cmd in j.commands)
                _commands[cmd.Key] = cmd;

            foreach (CommandGroup grp in j.groups)
            {
                foreach (string cmd in grp._commands)
                {
                    var command = GetCommand(cmd);
                    
                    // this ignores invalid commands; should we instead throw an error here?
                    if (command != null) grp.Commands.Add(command);
                }
                _groups[grp.Key] = grp;
            }

        }
    }

    public class CommandReferenceFile
    {
        [JsonProperty("commands")] public Command[] commands { get; set; }
        [JsonProperty("groups")] public CommandGroup[] groups { get; set; }        
    }

}
