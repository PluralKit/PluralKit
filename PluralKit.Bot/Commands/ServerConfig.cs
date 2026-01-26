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

    public ServerConfig(IDiscordCache cache)
    {
        _cache = cache;
    }

    private record PaginatedConfigItem(string Key, string Description, string? CurrentValue, string DefaultValue);
    private string EnabledDisabled(bool value) => value ? "enabled" : "disabled";

    public async Task ShowConfig(Context ctx)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");
        var items = new List<PaginatedConfigItem>();

        items.Add(new(
            "log cleanup",
            "Whether to clean up other bots' log channels",
            EnabledDisabled(ctx.GuildConfig!.LogCleanupEnabled),
            "disabled"
        ));

        items.Add(new(
            "invalid command error",
            "Whether to show an error message when an unknown command is sent",
            EnabledDisabled(ctx.GuildConfig!.InvalidCommandResponseEnabled),
            "enabled"
        ));

        items.Add(new(
            "require tag",
            "Whether server users are required to have a system tag on proxied messages",
            EnabledDisabled(ctx.GuildConfig!.RequireSystemTag),
            "disabled"
        ));

        items.Add(new(
            "suppress notifications",
            "Whether all proxied messages will have notifications suppressed (sent as `@silent` messages)",
            EnabledDisabled(ctx.GuildConfig!.SuppressNotifications),
            "disabled"
        ));

        items.Add(new(
            "log channel",
            "Channel to log proxied messages to",
            ctx.GuildConfig!.LogChannel != null ? $"<#{ctx.GuildConfig.LogChannel}>" : "none",
            "none"
        ));

        string ChannelListMessage(int count, string cmd) => $"{count} channels, use `{ctx.DefaultPrefix}scfg {cmd}` to view/update";

        items.Add(new(
            "log blacklist",
            "Channels whose proxied messages will not be logged",
            ChannelListMessage(ctx.GuildConfig!.LogBlacklist.Length, "log blacklist"),
            ChannelListMessage(0, "log blacklist")
        ));

        items.Add(new(
            "proxy blacklist",
            "Channels where message proxying is disabled",
            ChannelListMessage(ctx.GuildConfig!.Blacklist.Length, "proxy blacklist"),
            ChannelListMessage(0, "proxy blacklist")
        ));

        await ctx.Paginate<PaginatedConfigItem>(
            items.ToAsyncEnumerable(),
            items.Count,
            10,
            "Current settings for this server",
            null,
            (eb, l) =>
            {
                var description = new StringBuilder();

                foreach (var item in l)
                {
                    description.Append(item.Key.AsCode());
                    description.Append($" **({item.CurrentValue ?? item.DefaultValue})**");
                    if (item.CurrentValue != null && item.CurrentValue != item.DefaultValue)
                        description.Append("\ud83d\udd39");

                    description.AppendLine();
                    description.Append(item.Description);
                    description.AppendLine();
                    description.AppendLine();
                }

                eb.Description(description.ToString());

                // using *large* blue diamond here since it's easier to see in the small footer
                eb.Footer(new($"\U0001f537 means this setting was changed. Type `{ctx.DefaultPrefix}serverconfig <setting name> clear` to reset it to the default."));

                return Task.CompletedTask;
            }
        );
    }

    public async Task ShowLogChannel(Context ctx)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");
        var settings = await ctx.Repository.GetGuild(ctx.Guild.Id);

        if (settings.LogChannel == null)
        {
            await ctx.Reply("This server does not have a log channel set.");
            return;
        }

        await ctx.Reply($"This server's log channel is currently set to <#{settings.LogChannel}>.");
    }

    public async Task SetLogChannel(Context ctx, Channel channel)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        if (channel.GuildId != ctx.Guild.Id)
            throw Errors.ChannelNotFound(channel.Id.ToString());

        if (channel.Type != Channel.ChannelType.GuildText && channel.Type != Channel.ChannelType.GuildPublicThread && channel.Type != Channel.ChannelType.GuildPrivateThread)
            throw new PKError("PluralKit cannot log messages to this type of channel.");

        var perms = await _cache.BotPermissionsIn(ctx.Guild.Id, channel.Id);
        if (!perms.HasFlag(PermissionSet.SendMessages))
            throw new PKError("PluralKit is missing **Send Messages** permissions in the new log channel.");
        if (!perms.HasFlag(PermissionSet.EmbedLinks))
            throw new PKError("PluralKit is missing **Embed Links** permissions in the new log channel.");

        await ctx.Repository.UpdateGuild(ctx.Guild.Id, new GuildPatch { LogChannel = channel.Id });
        await ctx.Reply($"{Emojis.Success} Proxy logging channel set to <#{channel.Id}>.");
    }

    public async Task ClearLogChannel(Context ctx, bool confirmYes)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        if (!await ctx.ConfirmClear("the server log channel", confirmYes))
            return;

        await ctx.Repository.UpdateGuild(ctx.Guild.Id, new GuildPatch { LogChannel = null });
        await ctx.Reply($"{Emojis.Success} Proxy logging channel cleared.");
    }

    public async Task ShowProxyBlacklist(Context ctx)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var blacklist = await ctx.Repository.GetGuild(ctx.Guild.Id);

        // Resolve all channels from the cache and order by position
        var channels = (await Task.WhenAll(blacklist.Blacklist
                .Select(id => _cache.TryGetChannel(ctx.Guild.Id, id))))
            .Where(c => c != null)
            .OrderBy(c => c.Position)
            .ToList();

        if (channels.Count == 0)
        {
            await ctx.Reply("This server has no channels where proxying is disabled.");
            return;
        }

        await ctx.Paginate(channels.ToAsyncEnumerable(), channels.Count, 25,
            $"Blacklisted channels for {ctx.Guild.Name}",
            null,
            async (eb, l) =>
            {
                async Task<string> CategoryName(ulong? id) =>
                    id != null ? (await _cache.GetChannel(ctx.Guild.Id, id.Value)).Name : "(no category)";

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

    public async Task AddProxyBlacklist(Context ctx, Channel? channel, bool all)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var affectedChannels = new List<Channel>();
        if (all)
        {
            affectedChannels = (await _cache.GetGuildChannels(ctx.Guild.Id))
                .Where(x => DiscordUtils.IsValidGuildChannel(x)).ToList();
        }
        else if (channel != null)
        {
            if (channel.GuildId != ctx.Guild.Id)
                throw Errors.ChannelNotFound(channel.Id.ToString());
            affectedChannels.Add(channel);
        }
        else
        {
            throw new PKSyntaxError("You must specify a channel or use the --all flag.");
        }

        var guild = await ctx.Repository.GetGuild(ctx.Guild.Id);
        var blacklist = guild.Blacklist.ToHashSet();
        blacklist.UnionWith(affectedChannels.Select(c => c.Id));

        await ctx.Repository.UpdateGuild(ctx.Guild.Id, new GuildPatch { Blacklist = blacklist.ToArray() });

        await ctx.Reply($"{Emojis.Success} {(all ? "All channels" : "Channel")} added to the proxy blacklist.");
    }

    public async Task RemoveProxyBlacklist(Context ctx, Channel? channel, bool all)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var affectedChannels = new List<Channel>();
        if (all)
        {
            affectedChannels = (await _cache.GetGuildChannels(ctx.Guild.Id))
                .Where(x => DiscordUtils.IsValidGuildChannel(x)).ToList();
        }
        else if (channel != null)
        {
            if (channel.GuildId != ctx.Guild.Id)
                throw Errors.ChannelNotFound(channel.Id.ToString());
            affectedChannels.Add(channel);
        }
        else
        {
            throw new PKSyntaxError("You must specify a channel or use the --all flag.");
        }

        var guild = await ctx.Repository.GetGuild(ctx.Guild.Id);
        var blacklist = guild.Blacklist.ToHashSet();
        blacklist.ExceptWith(affectedChannels.Select(c => c.Id));

        await ctx.Repository.UpdateGuild(ctx.Guild.Id, new GuildPatch { Blacklist = blacklist.ToArray() });

        await ctx.Reply($"{Emojis.Success} {(all ? "All channels" : "Channel")} removed from the proxy blacklist.");
    }

    public async Task ShowLogBlacklist(Context ctx)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var config = await ctx.Repository.GetGuild(ctx.Guild.Id);

        // Resolve all channels from the cache and order by position
        var channels = (await Task.WhenAll(config.LogBlacklist
                .Select(id => _cache.TryGetChannel(ctx.Guild.Id, id))))
            .Where(c => c != null)
            .OrderBy(c => c.Position)
            .ToList();

        if (channels.Count == 0)
        {
            await ctx.Reply("This server has no channels where logging is disabled.");
            return;
        }

        await ctx.Paginate(channels.ToAsyncEnumerable(), channels.Count, 25,
            $"Channels where logging is disabled for {ctx.Guild.Name}",
            null,
            async (eb, l) =>
            {
                async Task<string> CategoryName(ulong? id) =>
                    id != null ? (await _cache.GetChannel(ctx.Guild.Id, id.Value)).Name : "(no category)";

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

    public async Task AddLogBlacklist(Context ctx, Channel? channel, bool all)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var affectedChannels = new List<Channel>();
        if (all)
        {
            affectedChannels = (await _cache.GetGuildChannels(ctx.Guild.Id))
                .Where(x => DiscordUtils.IsValidGuildChannel(x)).ToList();
        }
        else if (channel != null)
        {
            if (channel.GuildId != ctx.Guild.Id)
                throw Errors.ChannelNotFound(channel.Id.ToString());
            affectedChannels.Add(channel);
        }
        else
        {
            throw new PKSyntaxError("You must specify a channel or use the --all flag.");
        }

        var guild = await ctx.Repository.GetGuild(ctx.Guild.Id);
        var blacklist = guild.LogBlacklist.ToHashSet();
        blacklist.UnionWith(affectedChannels.Select(c => c.Id));

        await ctx.Repository.UpdateGuild(ctx.Guild.Id, new GuildPatch { LogBlacklist = blacklist.ToArray() });

        await ctx.Reply(
            $"{Emojis.Success} {(all ? "All channels" : "Channel")} added to the logging blacklist." +
            (guild.LogChannel == null
                ? $"\n{Emojis.Warn} Please note that no logging channel is set, so there is nowhere to log messages to. You can set a logging channel using `{ctx.DefaultPrefix}serverconfig log channel #your-log-channel`."
                : ""));
    }

    public async Task RemoveLogBlacklist(Context ctx, Channel? channel, bool all)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var affectedChannels = new List<Channel>();
        if (all)
        {
            affectedChannels = (await _cache.GetGuildChannels(ctx.Guild.Id))
                .Where(x => DiscordUtils.IsValidGuildChannel(x)).ToList();
        }
        else if (channel != null)
        {
            if (channel.GuildId != ctx.Guild.Id)
                throw Errors.ChannelNotFound(channel.Id.ToString());
            affectedChannels.Add(channel);
        }
        else
        {
            throw new PKSyntaxError("You must specify a channel or use the --all flag.");
        }

        var guild = await ctx.Repository.GetGuild(ctx.Guild.Id);
        var blacklist = guild.LogBlacklist.ToHashSet();
        blacklist.ExceptWith(affectedChannels.Select(c => c.Id));

        await ctx.Repository.UpdateGuild(ctx.Guild.Id, new GuildPatch { LogBlacklist = blacklist.ToArray() });

        await ctx.Reply(
            $"{Emojis.Success} {(all ? "All channels" : "Channel")} removed from the logging blacklist." +
            (guild.LogChannel == null
                ? $"\n{Emojis.Warn} Please note that no logging channel is set, so there is nowhere to log messages to. You can set a logging channel using `{ctx.DefaultPrefix}serverconfig log channel #your-log-channel`."
                : ""));
    }

    public async Task ShowLogCleanup(Context ctx)
    {
        var botList = string.Join(", ", LoggerCleanService.Bots.Select(b => b.Name).OrderBy(x => x.ToLowerInvariant()));
        var eb = new EmbedBuilder()
            .Title("Log cleanup settings")
            .Field(new Embed.Field("Supported bots", botList));

        if (ctx.Guild == null)
        {
            eb.Description("Run this command in a server to enable/disable log cleanup.");
            await ctx.Reply(embed: eb.Build());
            return;
        }

        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        if (ctx.GuildConfig!.LogCleanupEnabled)
            eb.Description(
                $"Log cleanup is currently **on** for this server. To disable it, type `{ctx.DefaultPrefix}serverconfig logclean off`.");
        else
            eb.Description(
                $"Log cleanup is currently **off** for this server. To enable it, type `{ctx.DefaultPrefix}serverconfig logclean on`.");

        await ctx.Reply(embed: eb.Build());
    }

    public async Task SetLogCleanup(Context ctx, bool value)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var botList = string.Join(", ", LoggerCleanService.Bots.Select(b => b.Name).OrderBy(x => x.ToLowerInvariant()));

        await ctx.Repository.UpdateGuild(ctx.Guild.Id, new GuildPatch { LogCleanupEnabled = value });

        if (value)
            await ctx.Reply(
                $"{Emojis.Success} Log cleanup has been **enabled** for this server. Messages deleted by PluralKit will now be cleaned up from logging channels managed by the following bots:\n- **{botList}**\n\n{Emojis.Note} Make sure PluralKit has the **Manage Messages** permission in the channels in question.\n{Emojis.Note} Also, make sure to blacklist the logging channel itself from the bots in question to prevent conflicts.");
        else
            await ctx.Reply($"{Emojis.Success} Log cleanup has been **disabled** for this server.");
    }

    public async Task ShowInvalidCommandResponse(Context ctx)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var msg = $"Error responses for unknown/invalid commands are currently **{EnabledDisabled(ctx.GuildConfig!.InvalidCommandResponseEnabled)}**.";
        await ctx.Reply(msg);
    }

    public async Task SetInvalidCommandResponse(Context ctx, bool value)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        await ctx.Repository.UpdateGuild(ctx.Guild.Id, new() { InvalidCommandResponseEnabled = value });
        await ctx.Reply($"Error responses for unknown/invalid commands are now {EnabledDisabled(value)}.");
    }

    public async Task ShowRequireSystemTag(Context ctx)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var msg = $"System tags are currently **{(ctx.GuildConfig!.RequireSystemTag ? "required" : "not required")}** for PluralKit users in this server.";
        await ctx.Reply(msg);
    }

    public async Task SetRequireSystemTag(Context ctx, bool value)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        await ctx.Repository.UpdateGuild(ctx.Guild.Id, new() { RequireSystemTag = value });
        await ctx.Reply($"System tags are now **{(value ? "required" : "not required")}** for PluralKit users in this server.");
    }

    public async Task ShowSuppressNotifications(Context ctx)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        var msg = $"Suppressing notifications for proxied messages is currently **{EnabledDisabled(ctx.GuildConfig!.SuppressNotifications)}**.";
        await ctx.Reply(msg);
    }

    public async Task SetSuppressNotifications(Context ctx, bool value)
    {
        await ctx.CheckGuildContext().CheckAuthorPermission(PermissionSet.ManageGuild, "Manage Server");

        await ctx.Repository.UpdateGuild(ctx.Guild.Id, new() { SuppressNotifications = value });
        await ctx.Reply($"Suppressing notifications for proxied messages is now {EnabledDisabled(value)}.");
    }
}