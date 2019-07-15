using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace PluralKit.Bot.Commands
{
    public class ModCommands: ModuleBase<PKCommandContext>
    {
        public LogChannelService LogChannels { get; set; }
        public MessageStore Messages { get; set; }
        
        public EmbedService Embeds { get; set; }
        
        [Command("log")]
        [Remarks("log <channel>")]
        [RequireUserPermission(GuildPermission.ManageGuild, ErrorMessage = "You must have the Manage Server permission to use this command.")]
        [RequireContext(ContextType.Guild, ErrorMessage = "This command can not be run in a DM.")]
        public async Task SetLogChannel(ITextChannel channel = null)
        {
            await LogChannels.SetLogChannel(Context.Guild, channel);

            if (channel != null)
                await Context.Channel.SendMessageAsync($"{Emojis.Success} Proxy logging channel set to #{channel.Name.Sanitize()}.");
            else
                await Context.Channel.SendMessageAsync($"{Emojis.Success} Proxy logging channel cleared.");
        }

        [Command("message")]
        [Remarks("message <messageid>")]
        [Alias("msg")]
        public async Task GetMessage(ulong messageId)
        {
            var message = await Messages.Get(messageId);
            if (message == null) throw Errors.MessageNotFound(messageId);

            await Context.Channel.SendMessageAsync(embed: await Embeds.CreateMessageInfoEmbed(message));
        }

        [Command("message")]
        [Remarks("message <messageid>")]
        [Alias("msg")]
        public async Task GetMessage(IMessage msg) => await GetMessage(msg.Id);
    }
}