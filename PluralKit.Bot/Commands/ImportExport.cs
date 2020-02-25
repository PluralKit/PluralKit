using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Net;

using Newtonsoft.Json;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class ImportExport
    {
        private DataFileService _dataFiles;
        public ImportExport(DataFileService dataFiles)
        {
            _dataFiles = dataFiles;
        }

        public async Task Import(Context ctx)
        {
            var url = ctx.RemainderOrNull() ?? ctx.Message.Attachments.FirstOrDefault()?.Url;
            if (url == null) throw Errors.NoImportFilePassed;

            await ctx.BusyIndicator(async () =>
            {
                using (var client = new HttpClient())
                {
                    HttpResponseMessage response;
                    try
                    {
                         response = await client.GetAsync(url);
                    }
                    catch (InvalidOperationException)
                    {
                        // Invalid URL throws this, we just error back out
                        throw Errors.InvalidImportFile;
                    }

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
                            if (res.HadGroups || res.HadIndividualTags)
                            {
                                var issueStr =
                                    $"{Emojis.Warn} The following potential issues were detected converting your Tupperbox input file:";
                                if (res.HadGroups)
                                    issueStr +=
                                        "\n- PluralKit does not support member groups. Members will be imported without groups.";
                                if (res.HadIndividualTags)
                                    issueStr +=
                                        "\n- PluralKit does not support per-member system tags. Since you had multiple members with distinct tags, those tags will be applied to the members' *display names*/nicknames instead.";

                                var msg = await ctx.Reply($"{issueStr}\n\nDo you want to proceed with the import?");
                                if (!await ctx.PromptYesNo(msg)) throw Errors.ImportCancelled;
                            }
                            
                            data = res.System;
                        }
                        catch (JsonException)
                        {
                            throw Errors.InvalidImportFile;
                        }
                    }
                    
                    
                    if (!data.Valid) throw Errors.InvalidImportFile;

                    if (data.LinkedAccounts != null && !data.LinkedAccounts.Contains(ctx.Author.Id))
                    {
                        var msg = await ctx.Reply($"{Emojis.Warn} You seem to importing a system profile belonging to another account. Are you sure you want to proceed?");
                        if (!await ctx.PromptYesNo(msg)) throw Errors.ImportCancelled;
                    }

                    // If passed system is null, it'll create a new one
                    // (and that's okay!)
                    var result = await _dataFiles.ImportSystem(data, ctx.System, ctx.Author.Id);
                    if (!result.Success)
                        await ctx.Reply($"{Emojis.Error} The provided system profile could not be imported. {result.Message}");
                    else if (ctx.System == null)
                    {
                        // We didn't have a system prior to importing, so give them the new system's ID
                        await ctx.Reply($"{Emojis.Success} PluralKit has created a system for you based on the given file. Your system ID is `{result.System.Hid}`. Type `pk;system` for more information.");
                    }
                    else
                    {
                        // We already had a system, so show them what changed
                        await ctx.Reply($"{Emojis.Success} Updated {result.ModifiedNames.Count} members, created {result.AddedNames.Count} members. Type `pk;system list` to check!");
                    }
                }
            });
        }
        
        public async Task Export(Context ctx)
        {
            ctx.CheckSystem();
            
            var json = await ctx.BusyIndicator(async () =>
            {
                // Make the actual data file
                var data = await _dataFiles.ExportSystem(ctx.System);
                return JsonConvert.SerializeObject(data, Formatting.None);
            });
            
                            
            // Send it as a Discord attachment *in DMs*
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            try
            {
                await ctx.Author.SendFileAsync(stream, "system.json", $"{Emojis.Success} Here you go!");
                
                // If the original message wasn't posted in DMs, send a public reminder
                if (!(ctx.Channel is IDMChannel))
                    await ctx.Reply($"{Emojis.Success} Check your DMs!");
            }
            catch (HttpException)
            {
                // If user has DMs closed, tell 'em to open them
                await ctx.Reply(
                    $"{Emojis.Error} Could not send the data file in your DMs. Do you have DMs closed?");
            }
        }
    }
}