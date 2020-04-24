using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class ServerConfig
    {
        private IDataStore _data;
        private LoggerCleanService _cleanService;
        public ServerConfig(IDataStore data, LoggerCleanService cleanService)
        {
            _data = data;
            _cleanService = cleanService;
        }

        public async Task SetLogChannel(Context ctx)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(Permissions.ManageGuild, "Manage Server");
            
            DiscordChannel channel = null;
            if (ctx.HasNext())
                channel = ctx.MatchChannel() ?? throw new PKSyntaxError("You must pass a #channel to set.");
            if (channel != null && channel.GuildId != ctx.Guild.Id) throw new PKError("That channel is not in this server!");
            if (channel.Type != ChannelType.Text) throw new PKError("The logging channel must be a text channel."); //TODO: test this

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
            ctx.CheckGuildContext().CheckAuthorPermission(Permissions.ManageGuild, "Manage Server");

            var affectedChannels = new List<DiscordChannel>();
            if (ctx.Match("all"))
                affectedChannels = (await ctx.Guild.GetChannelsAsync()).Where(x => x.Type == ChannelType.Text).ToList();
            else if (!ctx.HasNext()) throw new PKSyntaxError("You must pass one or more #channels.");
            else while (ctx.HasNext())
            {
                var channel = ctx.MatchChannel(); //TODO: test this
                if (channel.Type != ChannelType.Text)
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
            ctx.CheckGuildContext().CheckAuthorPermission(Permissions.ManageGuild, "Manage Server");

            var affectedChannels = new List<DiscordChannel>();
            if (ctx.Match("all"))
                affectedChannels = (await ctx.Guild.GetChannelsAsync()).Where(x => x.Type == ChannelType.Text).ToList();
            else if (!ctx.HasNext()) throw new PKSyntaxError("You must pass one or more #channels.");
            else while (ctx.HasNext())
            {
                var channel = ctx.MatchChannel(); //TODO: test this
                if (channel.Type != ChannelType.Text)
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

        public async Task SetLogCleanup(Context ctx)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(Permissions.ManageGuild, "Manage Server");

            var guildCfg = await _data.GetOrCreateGuildConfig(ctx.Guild.Id);
            var botList = string.Join(", ", _cleanService.Bots.Select(b => b.Name).OrderBy(x => x.ToLowerInvariant()));
            
            if (ctx.Match("enable", "on", "yes"))
            {
                guildCfg.LogCleanupEnabled = true;
                await _data.SaveGuildConfig(guildCfg);
                await ctx.Reply($"{Emojis.Success} Log cleanup has been **enabled** for this server. Messages deleted by PluralKit will now be cleaned up from logging channels managed by the following bots:\n- **{botList}**\n\n{Emojis.Note} Make sure PluralKit has the **Manage Messages** permission in the channels in question.\n{Emojis.Note} Also, make sure to blacklist the logging channel itself from the bots in question to prevent conflicts.");
            }
            else if (ctx.Match("disable", "off", "no"))
            {
                guildCfg.LogCleanupEnabled = false;
                await _data.SaveGuildConfig(guildCfg);
                await ctx.Reply($"{Emojis.Success} Log cleanup has been **disabled** for this server.");
            }
            else
            {
                var eb = new DiscordEmbedBuilder()
                    .WithTitle("Log cleanup settings")
                    .AddField("Supported bots", botList);
                
                if (guildCfg.LogCleanupEnabled)
                    eb.WithDescription("Log cleanup is currently **on** for this server. To disable it, type `pk;logclean off`."); 
                else 
                    eb.WithDescription("Log cleanup is currently **off** for this server. To enable it, type `pk;logclean on`.");
                await ctx.Reply(embed: eb.Build());
            }
        }
    }
}