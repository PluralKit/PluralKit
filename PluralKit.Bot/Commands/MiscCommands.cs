using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace PluralKit.Bot.Commands {
    public class MiscCommands: ModuleBase<PKCommandContext> {
        [Command("invite")]
        [Remarks("invite")]
        public async Task Invite() {
            var info = await Context.Client.GetApplicationInfoAsync();

            var permissions = new GuildPermissions(
                addReactions: true,
                attachFiles: true,
                embedLinks: true,
                manageMessages: true,
                manageWebhooks: true,
                readMessageHistory: true,
                sendMessages: true
            );

            // TODO: allow customization of invite ID
            var invite = $"https://discordapp.com/oauth2/authorize?client_id={info.Id}&scope=bot&permissions={permissions.RawValue}";
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Use this link to add PluralKit to your server:\n<{invite}>");
        }
    }
}