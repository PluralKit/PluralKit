using System;
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
        private IDatabase _db;
        private LoggerCleanService _cleanService;
        public ServerConfig(LoggerCleanService cleanService, IDatabase db)
        {
            _cleanService = cleanService;
            _db = db;
        }

        public async Task SetLogChannel(Context ctx)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(Permissions.ManageGuild, "Manage Server");
            
            DiscordChannel channel = null;
            if (ctx.HasNext())
                channel = await ctx.MatchChannel() ?? throw new PKSyntaxError("You must pass a #channel to set.");
            if (channel != null && channel.GuildId != ctx.Guild.Id) throw new PKError("That channel is not in this server!");

            var patch = new GuildPatch {LogChannel = channel?.Id};
            await _db.Execute(conn => conn.UpsertGuild(ctx.Guild.Id, patch));

            if (channel != null)
                await ctx.Reply($"{Emojis.Success} Proxy logging channel set to #{channel.Name}.");
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
                var channel = await ctx.MatchChannel() ?? throw new PKSyntaxError($"Channel \"{ctx.PopArgument()}\" not found.");
                if (channel.GuildId != ctx.Guild.Id) throw new PKError($"Channel {ctx.Guild.Id} is not in this server.");
                affectedChannels.Add(channel);
            }

            ulong? logChannel = null;
            await using (var conn = await _db.Obtain())
            {
                var config = await conn.QueryOrInsertGuildConfig(ctx.Guild.Id);
                logChannel = config.LogChannel;
                var blacklist = config.LogBlacklist.ToHashSet();
                if (enable)
                    blacklist.ExceptWith(affectedChannels.Select(c => c.Id));
                else
                    blacklist.UnionWith(affectedChannels.Select(c => c.Id));
                
                var patch = new GuildPatch {LogBlacklist = blacklist.ToArray()};
                await conn.UpsertGuild(ctx.Guild.Id, patch);
            }

            await ctx.Reply(
                $"{Emojis.Success} Message logging for the given channels {(enable ? "enabled" : "disabled")}." +
                (logChannel == null ? $"\n{Emojis.Warn} Please note that no logging channel is set, so there is nowhere to log messages to. You can set a logging channel using `pk;log channel #your-log-channel`." : ""));
        }

        public async Task ShowBlacklisted(Context ctx)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(Permissions.ManageGuild, "Manage Server");

            var blacklist = await _db.Execute(c => c.QueryOrInsertGuildConfig(ctx.Guild.Id));
            
            // Resolve all channels from the cache and order by position
            var channels = blacklist.Blacklist
                .Select(id => ctx.Guild.GetChannel(id))
                .OrderBy(c => c.Position)
                .ToList();

            if (channels.Count == 0)
            {
                await ctx.Reply($"This server has no blacklisted channels.");
                return;
            }

            await ctx.Paginate(channels.ToAsyncEnumerable(), channels.Count, 25,
                $"Blacklisted channels for {ctx.Guild.Name}",
                (eb, l) =>
                {
                    eb.Description = string.Join("\n", l.Select(c => c.Mention));
                    return Task.CompletedTask;
                });
        }

        public async Task SetBlacklisted(Context ctx, bool shouldAdd)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(Permissions.ManageGuild, "Manage Server");

            var affectedChannels = new List<DiscordChannel>();
            if (ctx.Match("all"))
                affectedChannels = (await ctx.Guild.GetChannelsAsync()).Where(x => x.Type == ChannelType.Text).ToList();
            else if (!ctx.HasNext()) throw new PKSyntaxError("You must pass one or more #channels.");
            else while (ctx.HasNext())
            {
                var channel = await ctx.MatchChannel() ?? throw new PKSyntaxError($"Channel \"{ctx.PopArgument()}\" not found.");
                if (channel.GuildId != ctx.Guild.Id) throw new PKError($"Channel {ctx.Guild.Id} is not in this server.");
                affectedChannels.Add(channel);
            }
            
            await using (var conn = await _db.Obtain())
            {
                var guild = await conn.QueryOrInsertGuildConfig(ctx.Guild.Id);
                var blacklist = guild.Blacklist.ToHashSet();
                if (shouldAdd)
                    blacklist.UnionWith(affectedChannels.Select(c => c.Id));
                else
                    blacklist.ExceptWith(affectedChannels.Select(c => c.Id));
                
                var patch = new GuildPatch {Blacklist = blacklist.ToArray()};
                await conn.UpsertGuild(ctx.Guild.Id, patch);
            }

            await ctx.Reply($"{Emojis.Success} Channels {(shouldAdd ? "added to" : "removed from")} the proxy blacklist.");
        }

        public async Task SetLogCleanup(Context ctx)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(Permissions.ManageGuild, "Manage Server");

            var botList = string.Join(", ", _cleanService.Bots.Select(b => b.Name).OrderBy(x => x.ToLowerInvariant()));

            bool newValue;
            if (ctx.Match("enable", "on", "yes"))
                newValue = true;
            else if (ctx.Match("disable", "off", "no"))
                newValue = false;
            else
            {
                var eb = new DiscordEmbedBuilder()
                    .WithTitle("Log cleanup settings")
                    .AddField("Supported bots", botList);

                var guildCfg = await _db.Execute(c => c.QueryOrInsertGuildConfig(ctx.Guild.Id));
                if (guildCfg.LogCleanupEnabled)
                    eb.WithDescription("Log cleanup is currently **on** for this server. To disable it, type `pk;logclean off`."); 
                else 
                    eb.WithDescription("Log cleanup is currently **off** for this server. To enable it, type `pk;logclean on`.");
                await ctx.Reply(embed: eb.Build());
                return;
            }

            var patch = new GuildPatch {LogCleanupEnabled = newValue};
            await _db.Execute(conn => conn.UpsertGuild(ctx.Guild.Id, patch));

            if (newValue)
                await ctx.Reply($"{Emojis.Success} Log cleanup has been **enabled** for this server. Messages deleted by PluralKit will now be cleaned up from logging channels managed by the following bots:\n- **{botList}**\n\n{Emojis.Note} Make sure PluralKit has the **Manage Messages** permission in the channels in question.\n{Emojis.Note} Also, make sure to blacklist the logging channel itself from the bots in question to prevent conflicts.");
            else
                await ctx.Reply($"{Emojis.Success} Log cleanup has been **disabled** for this server.");
        }
    }
}