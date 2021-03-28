using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class ServerConfig
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly IDiscordCache _cache;
        private readonly LoggerCleanService _cleanService;
        public ServerConfig(LoggerCleanService cleanService, IDatabase db, ModelRepository repo, IDiscordCache cache)
        {
            _cleanService = cleanService;
            _db = db;
            _repo = repo;
            _cache = cache;
        }

        public async Task SetLogChannel(Context ctx)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");
            
            if (await ctx.MatchClear("the server log channel"))
            {
                await _db.Execute(conn => _repo.UpsertGuild(conn, ctx.Guild.Id, new GuildPatch {LogChannel = null}));
                await ctx.Reply($"{Emojis.Success} Proxy logging channel cleared.");
                return;
            }
            
            if (!ctx.HasNext())
                throw new PKSyntaxError("You must pass a #channel to set, or `clear` to clear it.");
            
            Channel channel = null;
            var channelString = ctx.PeekArgument();
            channel = await ctx.MatchChannel();
            if (channel == null || channel.GuildId != ctx.Guild.Id) throw Errors.ChannelNotFound(channelString);

            var patch = new GuildPatch {LogChannel = channel.Id};
            await _db.Execute(conn => _repo.UpsertGuild(conn, ctx.Guild.Id, patch));
            await ctx.Reply($"{Emojis.Success} Proxy logging channel set to #{channel.Name}.");
        }

        public async Task SetLogEnabled(Context ctx, bool enable)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

            var affectedChannels = new List<Channel>();
            if (ctx.Match("all"))
                affectedChannels = _cache.GetGuildChannels(ctx.Guild.Id).Where(x => x.Type == Channel.ChannelType.GuildText).ToList();
            else if (!ctx.HasNext()) throw new PKSyntaxError("You must pass one or more #channels.");
            else while (ctx.HasNext())
            {
                var channelString = ctx.PeekArgument();
                var channel = await ctx.MatchChannel();
                if (channel == null || channel.GuildId != ctx.Guild.Id) throw Errors.ChannelNotFound(channelString);
                affectedChannels.Add(channel);
            }

            ulong? logChannel = null;
            await using (var conn = await _db.Obtain())
            {
                var config = await _repo.GetGuild(conn, ctx.Guild.Id);
                logChannel = config.LogChannel;
                var blacklist = config.LogBlacklist.ToHashSet();
                if (enable)
                    blacklist.ExceptWith(affectedChannels.Select(c => c.Id));
                else
                    blacklist.UnionWith(affectedChannels.Select(c => c.Id));
                
                var patch = new GuildPatch {LogBlacklist = blacklist.ToArray()};
                await _repo.UpsertGuild(conn, ctx.Guild.Id, patch);
            }

            await ctx.Reply(
                $"{Emojis.Success} Message logging for the given channels {(enable ? "enabled" : "disabled")}." +
                (logChannel == null ? $"\n{Emojis.Warn} Please note that no logging channel is set, so there is nowhere to log messages to. You can set a logging channel using `pk;log channel #your-log-channel`." : ""));
        }

        public async Task ShowBlacklisted(Context ctx)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

            var blacklist = await _db.Execute(c => _repo.GetGuild(c, ctx.Guild.Id));
            
            // Resolve all channels from the cache and order by position
            var channels = blacklist.Blacklist
                .Select(id => _cache.GetChannelOrNull(id))
                .Where(c => c != null)
                .OrderBy(c => c.Position)
                .ToList();

            if (channels.Count == 0)
            {
                await ctx.Reply($"This server has no blacklisted channels.");
                return;
            }

            await ctx.Paginate(channels.ToAsyncEnumerable(), channels.Count, 25,
                $"Blacklisted channels for {ctx.Guild.Name}",
                null,
                (eb, l) =>
                {
                    string CategoryName(ulong? id) =>
                        id != null ? _cache.GetChannel(id.Value).Name : "(no category)";
                    
                    ulong? lastCategory = null;

                    var fieldValue = new StringBuilder();
                    foreach (var channel in l)
                    {
                        if (lastCategory != channel!.ParentId && fieldValue.Length > 0)
                        {
                            eb.Field(new(CategoryName(lastCategory), fieldValue.ToString()));
                            fieldValue.Clear();
                        }
                        else fieldValue.Append("\n");

                        fieldValue.Append(channel.Mention());
                        lastCategory = channel.ParentId;
                    }

                    eb.Field(new(CategoryName(lastCategory), fieldValue.ToString()));

                    return Task.CompletedTask;
                });
        }

        public async Task SetBlacklisted(Context ctx, bool shouldAdd)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

            var affectedChannels = new List<Channel>();
            if (ctx.Match("all"))
                affectedChannels = _cache.GetGuildChannels(ctx.Guild.Id).Where(x => x.Type == Channel.ChannelType.GuildText).ToList();
            else if (!ctx.HasNext()) throw new PKSyntaxError("You must pass one or more #channels.");
            else while (ctx.HasNext())
            {
                var channelString = ctx.PeekArgument();
                var channel = await ctx.MatchChannel();
                if (channel == null || channel.GuildId != ctx.Guild.Id) throw Errors.ChannelNotFound(channelString);
                affectedChannels.Add(channel);
            }
            
            await using (var conn = await _db.Obtain())
            {
                var guild = await _repo.GetGuild(conn, ctx.Guild.Id);
                var blacklist = guild.Blacklist.ToHashSet();
                if (shouldAdd)
                    blacklist.UnionWith(affectedChannels.Select(c => c.Id));
                else
                    blacklist.ExceptWith(affectedChannels.Select(c => c.Id));
                
                var patch = new GuildPatch {Blacklist = blacklist.ToArray()};
                await _repo.UpsertGuild(conn, ctx.Guild.Id, patch);
            }

            await ctx.Reply($"{Emojis.Success} Channels {(shouldAdd ? "added to" : "removed from")} the proxy blacklist.");
        }

        public async Task SetLogCleanup(Context ctx)
        {
            ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

            var botList = string.Join(", ", _cleanService.Bots.Select(b => b.Name).OrderBy(x => x.ToLowerInvariant()));

            bool newValue;
            if (ctx.Match("enable", "on", "yes"))
                newValue = true;
            else if (ctx.Match("disable", "off", "no"))
                newValue = false;
            else
            {
                var eb = new EmbedBuilder()
                    .Title("Log cleanup settings")
                    .Field(new("Supported bots", botList));

                var guildCfg = await _db.Execute(c => _repo.GetGuild(c, ctx.Guild.Id));
                if (guildCfg.LogCleanupEnabled)
                    eb.Description("Log cleanup is currently **on** for this server. To disable it, type `pk;logclean off`."); 
                else 
                    eb.Description("Log cleanup is currently **off** for this server. To enable it, type `pk;logclean on`.");
                await ctx.Reply(embed: eb.Build());
                return;
            }

            var patch = new GuildPatch {LogCleanupEnabled = newValue};
            await _db.Execute(conn => _repo.UpsertGuild(conn, ctx.Guild.Id, patch));

            if (newValue)
                await ctx.Reply($"{Emojis.Success} Log cleanup has been **enabled** for this server. Messages deleted by PluralKit will now be cleaned up from logging channels managed by the following bots:\n- **{botList}**\n\n{Emojis.Note} Make sure PluralKit has the **Manage Messages** permission in the channels in question.\n{Emojis.Note} Also, make sure to blacklist the logging channel itself from the bots in question to prevent conflicts.");
            else
                await ctx.Reply($"{Emojis.Success} Log cleanup has been **disabled** for this server.");
        }
    }
}