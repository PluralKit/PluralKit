using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using DSharpPlus.Exceptions;
using DSharpPlus.Entities;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class ImportExport
    {
        private readonly DataFileService _dataFiles;
        private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            // Otherwise it'll mess up/reformat the ISO strings for ???some??? reason >.>
            DateParseHandling = DateParseHandling.None
        };
        
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

                    if (!response.IsSuccessStatusCode) 
                        throw Errors.InvalidImportFile;

                    DataFileSystem data;
                    try
                    {
                        var json = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync(), _settings);
                        data = await LoadSystem(ctx, json);
                    }
                    catch (JsonException)
                    {
                        throw Errors.InvalidImportFile;
                    }

                    if (!data.Valid) 
                        throw Errors.InvalidImportFile;

                    if (data.LinkedAccounts != null && !data.LinkedAccounts.Contains(ctx.Author.Id))
                    {
                        var msg = $"{Emojis.Warn} You seem to importing a system profile belonging to another account. Are you sure you want to proceed?";
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

        private async Task<DataFileSystem> LoadSystem(Context ctx, JObject json)
        {
            if (json.ContainsKey("tuppers"))
                return await ImportFromTupperbox(ctx, json);

            return json.ToObject<DataFileSystem>();
        }

        private async Task<DataFileSystem> ImportFromTupperbox(Context ctx, JObject json)
        {
            var tupperbox = json.ToObject<TupperboxProfile>();
            if (!tupperbox.Valid)
                throw Errors.InvalidImportFile;
                            
            var res = tupperbox.ToPluralKit();
            if (res.HadGroups || res.HadIndividualTags)
            {
                var issueStr =
                    $"{Emojis.Warn} The following potential issues were detected converting your Tupperbox input file:";
                if (res.HadGroups)
                    issueStr += "\n- PluralKit does not support member groups. Members will be imported without groups.";
                if (res.HadIndividualTags)
                    issueStr += "\n- PluralKit does not support per-member system tags. Since you had multiple members with distinct tags, those tags will be applied to the members' *display names*/nicknames instead.";

                var msg = $"{issueStr}\n\nDo you want to proceed with the import?";
                if (!await ctx.PromptYesNo(msg))
                    throw Errors.ImportCancelled;
            }

            return res.System;
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
                var dm = await ctx.Rest.CreateDmAsync(ctx.Author.Id);
                var msg = await dm.SendFileAsync("system.json", stream, $"{Emojis.Success} Here you go!");
                await dm.SendMessageAsync($"<{msg.Attachments[0].Url}>");
                
                // If the original message wasn't posted in DMs, send a public reminder
                if (!(ctx.Channel is DiscordDmChannel))
                    await ctx.Reply($"{Emojis.Success} Check your DMs!");
            }
            catch (UnauthorizedException)
            {
                // If user has DMs closed, tell 'em to open them
                await ctx.Reply(
                    $"{Emojis.Error} Could not send the data file in your DMs. Do you have DMs closed?");
            }
        }
    }
}