using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class ModCommands
    {
        private LogChannelService _logChannels;
        private IDataStore _data;

        private EmbedService _embeds;

        public ModCommands(LogChannelService logChannels, IDataStore data, EmbedService embeds)
        {
            _logChannels = logChannels;
            _data = data;
            _embeds = embeds;
        }

        public async Task SetLogChannel(Context ctx)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(GuildPermission.ManageGuild, "Manage Server");
            
            ITextChannel channel = null;
            if (ctx.HasNext())
                channel = ctx.MatchChannel() ?? throw new PKSyntaxError("You must pass a #channel to set.");

            var cfg = await _data.GetGuildConfig(ctx.Guild.Id);
            cfg.LogChannel = channel?.Id;
            await _data.SaveGuildConfig(cfg);
            
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

            var message = await _data.GetMessage(messageId);
            if (message == null) throw Errors.MessageNotFound(messageId);

            await ctx.Reply(embed: await _embeds.CreateMessageInfoEmbed(message));
        }
    }
}