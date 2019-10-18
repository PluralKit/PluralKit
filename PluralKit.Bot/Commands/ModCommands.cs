using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class ModCommands
    {
        private LogChannelService _logChannels;
        private MessageStore _messages;

        private EmbedService _embeds;

        public ModCommands(LogChannelService logChannels, MessageStore messages, EmbedService embeds)
        {
            _logChannels = logChannels;
            _messages = messages;
            _embeds = embeds;
        }

        public async Task SetLogChannel(Context ctx)
        {
            ctx.CheckAuthorPermission(GuildPermission.ManageGuild, "Manage Server").CheckGuildContext();
            
            ITextChannel channel = null;
            if (ctx.HasNext())
                channel = ctx.MatchChannel() ?? throw new PKSyntaxError("You must pass a #channel to set.");
            
            await _logChannels.SetLogChannel(ctx.Guild, channel);

            if (channel != null)
                await ctx.Reply($"{Emojis.Success} Proxy logging channel set to #{channel.Name.SanitizeMentions()}.");
            else
                await ctx.Reply($"{Emojis.Success} Proxy logging channel cleared.");
        }
        
        public async Task GetMessage(Context ctx)
        {
            var word = ctx.PopArgument() ?? throw new PKSyntaxError("You must pass a message ID or link.");

            ulong messageId;
            if (ulong.TryParse(word, out var id))
                messageId = id;
            else if (Regex.Match(word, "https://discordapp.com/channels/\\d+/(\\d+)") is Match match && match.Success)
                messageId = ulong.Parse(match.Groups[1].Value);
            else throw new PKSyntaxError($"Could not parse `{word}` as a message ID or link.");

            var message = await _messages.Get(messageId);
            if (message == null) throw Errors.MessageNotFound(messageId);

            await ctx.Reply(embed: await _embeds.CreateMessageInfoEmbed(message));
        }
    }
}