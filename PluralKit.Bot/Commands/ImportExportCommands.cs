using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Newtonsoft.Json;

namespace PluralKit.Bot.Commands
{
    public class ImportExportCommands : ModuleBase<PKCommandContext>
    {
        public DataFileService DataFiles { get; set; }

        [Command("import")]
        [Remarks("import [fileurl]")]
        public async Task Import([Remainder] string url = null)
        {
            if (url == null) url = Context.Message.Attachments.FirstOrDefault()?.Filename;
            if (url == null) throw Errors.NoImportFilePassed;

            await Context.BusyIndicator(async () =>
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode) throw Errors.InvalidImportFile;
                    var str = await response.Content.ReadAsStringAsync();

                    var data = TryDeserialize(str);
                    if (!data.HasValue || !data.Value.Valid) throw Errors.InvalidImportFile;

                    if (Context.SenderSystem != null && Context.SenderSystem.Hid != data.Value.Id)
                    {
                        // TODO: prompt "are you sure you want to import someone else's system?
                    }

                    // If passed system is null, it'll create a new one
                    // (and that's okay!)
                    var result = await DataFiles.ImportSystem(data.Value, Context.SenderSystem);
                    
                    if (Context.SenderSystem == null)
                    {
                        await Context.Channel.SendMessageAsync($"{Emojis.Success} PluralKit has created a system for you based on the given file. Your system ID is `{result.System.Hid}`. Type `pk;system` for more information.");
                    }
                    else
                    {
                        await Context.Channel.SendMessageAsync($"{Emojis.Success} Updated {result.ModifiedNames.Count} members, created {result.AddedNames.Count} members. Type `pk;system list` to check!");
                    }
                }
            });
        }

        [Command("export")]
        [Remarks("export")]
        [MustHaveSystem]
        public async Task Export()
        {
            await Context.BusyIndicator(async () =>
            {
                var data = await DataFiles.ExportSystem(Context.SenderSystem);
                var json = JsonConvert.SerializeObject(data, Formatting.None);
                
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                await Context.Channel.SendFileAsync(stream, "system.json", $"{Emojis.Success} Here you go!");
            });
        }

        private DataFileSystem? TryDeserialize(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<DataFileSystem>(json);
            }
            catch (JsonException e)
            {
                Console.WriteLine("uww");
            }

            return null;
        }
    }
}