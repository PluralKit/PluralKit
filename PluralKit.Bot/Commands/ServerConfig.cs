using System.Text;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class ServerConfig
{
    private readonly IDiscordCache _cache;
    private readonly LoggerCleanService _cleanService;
    private readonly ModelRepository _repo;

    public ServerConfig(LoggerCleanService cleanService, ModelRepository repo, IDiscordCache cache)
    {
        _cleanService = cleanService;
        _repo = repo;
        _cache = cache;
    }

    public async Task SetLogChannel(Context ctx)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");
        var settings = await _repo.GetGuild(ctx.Guild.Id);

        if (await ctx.MatchClear("the server log channel"))
        {
            await _repo.UpdateGuild(ctx.Guild.Id, new GuildPatch { LogChannel = null });
            await ctx.Reply($"{Emojis.Success} Proxy logging channel cleared.");
            return;
        }

        if (!ctx.HasNext())
        {
            if (settings.LogChannel == null)
            {
                await ctx.Reply("This server does not have a log channel set.");
                return;
            }

            await ctx.Reply($"This server's log channel is currently set to <#{settings.LogChannel}>.");
            return;
        }

        Channel channel = null;
        var channelString = ctx.PeekArgument();
        channel = await ctx.MatchChannel();
        if (channel == null || channel.GuildId != ctx.Guild.Id) throw Errors.ChannelNotFound(channelString);
        if (channel.Type != Channel.ChannelType.GuildText)
            throw new PKError("PluralKit cannot log messages to this type of channel.");

        var perms = await _cache.PermissionsIn(channel.Id);
        if (!perms.HasFlag(PermissionSet.SendMessages))
            throw new PKError("PluralKit is missing **Send Messages** permissions in the new log channel.");
        if (!perms.HasFlag(PermissionSet.EmbedLinks))
            throw new PKError("PluralKit is missing **Embed Links** permissions in the new log channel.");

        await _repo.UpdateGuild(ctx.Guild.Id, new GuildPatch { LogChannel = channel.Id });
        await ctx.Reply($"{Emojis.Success} Proxy logging channel set to <#{channel.Id}>.");
    }

    public async Task SetLogEnabled(Context ctx, bool enable)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var affectedChannels = new List<Channel>();
        if (ctx.Match("all"))
            affectedChannels = (await _cache.GetGuildChannels(ctx.Guild.Id))
                .Where(x => x.Type == Channel.ChannelType.GuildText).ToList();
        else if (!ctx.HasNext()) throw new PKSyntaxError("You must pass one or more #channels.");
        else
            while (ctx.HasNext())
            {
                var channelString = ctx.PeekArgument();
                var channel = await ctx.MatchChannel();
                if (channel == null || channel.GuildId != ctx.Guild.Id) throw Errors.ChannelNotFound(channelString);
                affectedChannels.Add(channel);
            }

        ulong? logChannel = null;
        var config = await _repo.GetGuild(ctx.Guild.Id);
        logChannel = config.LogChannel;

        var blacklist = config.LogBlacklist.ToHashSet();
        if (enable)
            blacklist.ExceptWith(affectedChannels.Select(c => c.Id));
        else
            blacklist.UnionWith(affectedChannels.Select(c => c.Id));

        await _repo.UpdateGuild(ctx.Guild.Id, new GuildPatch { LogBlacklist = blacklist.ToArray() });

        await ctx.Reply(
            $"{Emojis.Success} Message logging for the given channels {(enable ? "enabled" : "disabled")}." +
            (logChannel == null
                ? $"\n{Emojis.Warn} Please note that no logging channel is set, so there is nowhere to log messages to. You can set a logging channel using `pk;log channel #your-log-channel`."
                : ""));
    }

    public async Task ShowBlacklisted(Context ctx)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var blacklist = await _repo.GetGuild(ctx.Guild.Id);

        // Resolve all channels from the cache and order by position
        var channels = (await Task.WhenAll(blacklist.Blacklist
                .Select(id => _cache.TryGetChannel(id))))
            .Where(c => c != null)
            .OrderBy(c => c.Position)
            .ToList();

        if (channels.Count == 0)
        {
            await ctx.Reply("This server has no blacklisted channels.");
            return;
        }

        await ctx.Paginate(channels.ToAsyncEnumerable(), channels.Count, 25,
            $"Blacklisted channels for {ctx.Guild.Name}",
            null,
            async (eb, l) =>
            {
                async Task<string> CategoryName(ulong? id) =>
                    id != null ? (await _cache.GetChannel(id.Value)).Name : "(no category)";

                ulong? lastCategory = null;

                var fieldValue = new StringBuilder();
                foreach (var channel in l)
                {
                    if (lastCategory != channel!.ParentId && fieldValue.Length > 0)
                    {
                        eb.Field(new Embed.Field(await CategoryName(lastCategory), fieldValue.ToString()));
                        fieldValue.Clear();
                    }
                    else
                    {
                        fieldValue.Append("\n");
                    }

                    fieldValue.Append(channel.Mention());
                    lastCategory = channel.ParentId;
                }

                eb.Field(new Embed.Field(await CategoryName(lastCategory), fieldValue.ToString()));
            });
    }

    public async Task SetBlacklisted(Context ctx, bool shouldAdd)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var affectedChannels = new List<Channel>();
        if (ctx.Match("all"))
            affectedChannels = (await _cache.GetGuildChannels(ctx.Guild.Id))
                .Where(x => x.Type == Channel.ChannelType.GuildText).ToList();
        else if (!ctx.HasNext()) throw new PKSyntaxError("You must pass one or more #channels.");
        else
            while (ctx.HasNext())
            {
                var channelString = ctx.PeekArgument();
                var channel = await ctx.MatchChannel();
                if (channel == null || channel.GuildId != ctx.Guild.Id) throw Errors.ChannelNotFound(channelString);
                affectedChannels.Add(channel);
            }

        var guild = await _repo.GetGuild(ctx.Guild.Id);

        var blacklist = guild.Blacklist.ToHashSet();
        if (shouldAdd)
            blacklist.UnionWith(affectedChannels.Select(c => c.Id));
        else
            blacklist.ExceptWith(affectedChannels.Select(c => c.Id));

        await _repo.UpdateGuild(ctx.Guild.Id, new GuildPatch { Blacklist = blacklist.ToArray() });

        await ctx.Reply(
            $"{Emojis.Success} Channels {(shouldAdd ? "added to" : "removed from")} the proxy blacklist.");
    }

    public async Task SetLogCleanup(Context ctx)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var botList = string.Join(", ", _cleanService.Bots.Select(b => b.Name).OrderBy(x => x.ToLowerInvariant()));

        var guild = await _repo.GetGuild(ctx.Guild.Id);

        bool newValue;
        if (ctx.Match("enable", "on", "yes"))
        {
            newValue = true;
        }
        else if (ctx.Match("disable", "off", "no"))
        {
            newValue = false;
        }
        else
        {
            var eb = new EmbedBuilder()
                .Title("Log cleanup settings")
                .Field(new Embed.Field("Supported bots", botList));

            var guildCfg = await _repo.GetGuild(ctx.Guild.Id);
            if (guildCfg.LogCleanupEnabled)
                eb.Description(
                    "Log cleanup is currently **on** for this server. To disable it, type `pk;logclean off`.");
            else
                eb.Description(
                    "Log cleanup is currently **off** for this server. To enable it, type `pk;logclean on`.");
            await ctx.Reply(embed: eb.Build());
            return;
        }

        await _repo.UpdateGuild(ctx.Guild.Id, new GuildPatch { LogCleanupEnabled = newValue });

        if (newValue)
            await ctx.Reply(
                $"{Emojis.Success} Log cleanup has been **enabled** for this server. Messages deleted by PluralKit will now be cleaned up from logging channels managed by the following bots:\n- **{botList}**\n\n{Emojis.Note} Make sure PluralKit has the **Manage Messages** permission in the channels in question.\n{Emojis.Note} Also, make sure to blacklist the logging channel itself from the bots in question to prevent conflicts.");
        else
            await ctx.Reply($"{Emojis.Success} Log cleanup has been **disabled** for this server.");
    }
}