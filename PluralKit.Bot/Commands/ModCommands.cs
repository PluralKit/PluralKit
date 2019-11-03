using System.Collections.Generic;
using System.Linq;
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
            if (channel != null && channel.GuildId != ctx.Guild.Id) throw new PKError("That channel is not in this server!"); 

            var cfg = await _data.GetOrCreateGuildConfig(ctx.Guild.Id);
            cfg.LogChannel = channel?.Id;
            await _data.SaveGuildConfig(cfg);
            
            if (channel != null)
                await ctx.Reply($"{Emojis.Success} Proxy logging channel set to #{channel.Name.SanitizeMentions()}.");
            else
                await ctx.Reply($"{Emojis.Success} Proxy logging channel cleared.");
        }

        public async Task SetLogEnabled(Context ctx, bool enable)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(GuildPermission.ManageGuild, "Manage Server");

            var affectedChannels = new List<ITextChannel>();
            if (ctx.Match("all"))
                affectedChannels = (await ctx.Guild.GetChannelsAsync()).OfType<ITextChannel>().ToList();
            else if (!ctx.HasNext()) throw new PKSyntaxError("You must pass one or more #channels.");
            else while (ctx.HasNext())
            {
                if (!(ctx.MatchChannel() is ITextChannel channel))
                    throw new PKSyntaxError($"Channel \"{ctx.PopArgument().SanitizeMentions()}\" not found.");
                if (channel.GuildId != ctx.Guild.Id) throw new PKError($"Channel {ctx.Guild.Id} is not in this server.");
                affectedChannels.Add(channel);
            }
            
            var guildCfg = await _data.GetOrCreateGuildConfig(ctx.Guild.Id);
            if (enable) guildCfg.LogBlacklist.ExceptWith(affectedChannels.Select(c => c.Id));
            else guildCfg.LogBlacklist.UnionWith(affectedChannels.Select(c => c.Id));

            await _data.SaveGuildConfig(guildCfg);
            await ctx.Reply(
                $"{Emojis.Success} Message logging for the given channels {(enable ? "enabled" : "disabled")}." +
                (guildCfg.LogChannel == null ? $"\n{Emojis.Warn} Please note that no logging channel is set, so there is nowhere to log messages to. You can set a logging channel using `pk;log channel #your-log-channel`." : ""));
        }
        
        public async Task SetBlacklisted(Context ctx, bool onBlacklist)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(GuildPermission.ManageGuild, "Manage Server");

            var affectedChannels = new List<ITextChannel>();
            if (ctx.Match("all"))
                affectedChannels = (await ctx.Guild.GetChannelsAsync()).OfType<ITextChannel>().ToList();
            else if (!ctx.HasNext()) throw new PKSyntaxError("You must pass one or more #channels.");
            else while (ctx.HasNext())
            {
                if (!(ctx.MatchChannel() is ITextChannel channel))
                    throw new PKSyntaxError($"Channel \"{ctx.PopArgument().SanitizeMentions()}\" not found.");
                if (channel.GuildId != ctx.Guild.Id) throw new PKError($"Channel {ctx.Guild.Id} is not in this server.");
                affectedChannels.Add(channel);
            }
            
            var guildCfg = await _data.GetOrCreateGuildConfig(ctx.Guild.Id);
            if (onBlacklist) guildCfg.Blacklist.UnionWith(affectedChannels.Select(c => c.Id));
            else guildCfg.Blacklist.ExceptWith(affectedChannels.Select(c => c.Id));

            await _data.SaveGuildConfig(guildCfg);
            await ctx.Reply($"{Emojis.Success} Channels {(onBlacklist ? "added to" : "removed from")} the proxy blacklist.");

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