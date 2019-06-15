using System;
using System.IO;
using System.Linq;
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
                    var json = await response.Content.ReadAsStringAsync();

                    var settings = new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Error
                    };
                    

                    DataFileSystem data;
                    
                    // TODO: can we clean up this mess?
                    try
                    {
                        data = JsonConvert.DeserializeObject<DataFileSystem>(json, settings);
                    }
                    catch (JsonException)
                    {
                        try
                        {
                            var tupperbox = JsonConvert.DeserializeObject<TupperboxProfile>(json, settings);
                            if (!tupperbox.Valid) throw Errors.InvalidImportFile;
                            
                            var res = tupperbox.ToPluralKit();
                            if (res.HadGroups || res.HadMultibrackets || res.HadIndividualTags)
                            {
                                var issueStr =
                                    $"{Emojis.Warn} The following potential issues were detected converting your Tupperbox input file:";
                                if (res.HadGroups)
                                    issueStr +=
                                        "\n- PluralKit does not support member groups. Members will be imported without groups.";
                                if (res.HadMultibrackets)
                                    issueStr += "\n- PluralKit does not support members with multiple proxy tags. Only the first pair will be imported.";
                                if (res.HadIndividualTags)
                                    issueStr +=
                                        "\n- PluralKit does not support per-member system tags. Since you had multiple members with distinct tags, tags will not be imported. You can set your system tag using the `pk;system tag <tag>` command later.";

                                var msg = await Context.Channel.SendMessageAsync($"{issueStr}\n\nDo you want to proceed with the import?");
                                if (!await Context.PromptYesNo(msg)) throw Errors.ImportCancelled;
                            }
                            
                            data = res.System;
                        }
                        catch (JsonException)
                        {
                            throw Errors.InvalidImportFile;
                        }
                    }
                    
                    
                    if (!data.Valid) throw Errors.InvalidImportFile;

                    if (data.LinkedAccounts != null && !data.LinkedAccounts.Contains(Context.User.Id))
                    {
                        var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} You seem to importing a system profile belonging to another account. Are you sure you want to proceed?");
                        if (!await Context.PromptYesNo(msg)) throw Errors.ImportCancelled;
                    }

                    // If passed system is null, it'll create a new one
                    // (and that's okay!)
                    var result = await DataFiles.ImportSystem(data, Context.SenderSystem);
                    
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
    }
}