using Myriad.Builders;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class SystemEditLogic
{
    public async Task SetName(JointContext ctx, string name)
    {
        if (name.Length > Limits.MaxSystemNameLength)
            throw Errors.StringTooLongError("System name", name.Length, Limits.MaxSystemNameLength);

        await ctx.Repository.UpdateSystem(ctx.System.Id, new SystemPatch { Name = name });

        await ctx.Reply($"{Emojis.Success} System name changed (using {name.Length}/{Limits.MaxSystemNameLength} characters).");
    }
    public async Task ClearName(JointContext ctx)
    {
        await ctx.Repository.UpdateSystem(ctx.System.Id, new SystemPatch { Name = null });

        await ctx.Reply($"{Emojis.Success} System name cleared.");
    }
    public async Task ShowName(JointContext ctx, PKSystem target, ReplyFormat format)
    {
        ctx.CheckSystemPrivacy(target.Id, target.NamePrivacy);
        var isOwnSystem = target.Id == ctx.System?.Id;

        var noNameSetMessage = $"{(isOwnSystem ? "Your" : "This")} system does not have a name set.";
        if (isOwnSystem)
            noNameSetMessage += $" Type `{ctx.DefaultPrefix}system name <name>` to set one.";
        if (target.Name == null)
        {
            await ctx.Reply(noNameSetMessage);
            return;
        }

        switch (format)
        {
            case ReplyFormat.Raw:
                await ctx.Reply($"```\n{target.Name}\n```");
                return;
            case ReplyFormat.Plaintext:
                var eb = new EmbedBuilder()
                                .Description($"Showing name for system `{target.DisplayHid(ctx.Config)}`");
                await ctx.Reply(target.Name, embed: eb.Build());
                return;
            default:
                await ctx.Reply(
                $"{(isOwnSystem ? "Your" : "This")} system's name is currently **{target.Name}**."
                + (isOwnSystem ? $" Type `{ctx.DefaultPrefix}system name -clear` to clear it."
                + $" Using {target.Name.Length}/{Limits.MaxSystemNameLength} characters." : ""));
                return;
        }
    }

    public async Task SetServerName(JointContext ctx, string name, ulong guildId = 0)
    {
        if (guildId == 0)
        {
            ctx.CheckGuildContext();
            guildId = ctx.Guild.Id;
        }

        var guild = await ctx.Rest.GetGuildOrNull(guildId) ?? throw Errors.GuildNotFound(guildId);

        if (name.Length > Limits.MaxSystemNameLength)
            throw Errors.StringTooLongError("System name for this server", name.Length, Limits.MaxSystemNameLength);

        await ctx.Repository.UpdateSystemGuild(ctx.System.Id, guildId, new SystemGuildPatch { DisplayName = name });

        await ctx.Reply($"{Emojis.Success} System name for {(guildId == ctx.Guild?.Id ? "this server" : $"server \"{guild.Name}\"")} changed (using {name.Length}/{Limits.MaxSystemNameLength} characters).");
    }
    public async Task ClearServerName(JointContext ctx, ulong guildId = 0)
    {
        if (guildId == 0)
        {
            ctx.CheckGuildContext();
            guildId = ctx.Guild.Id;
        }

        var guild = await ctx.Rest.GetGuildOrNull(guildId) ?? throw Errors.GuildNotFound(guildId);

        await ctx.Repository.UpdateSystemGuild(ctx.System.Id, guildId, new SystemGuildPatch { DisplayName = null });

        await ctx.Reply($"{Emojis.Success} System name for {(guildId == ctx.Guild?.Id ? "this server" : $"server \"{guild.Name}\"")} cleared.");
    }
    public async Task ShowServerName(JointContext ctx, PKSystem target, ReplyFormat format, ulong guildId = 0)
    {
        if (guildId == 0)
        {
            ctx.CheckGuildContext();
            guildId = ctx.Guild.Id;
        }

        var guild = await ctx.Rest.GetGuildOrNull(guildId) ?? throw Errors.GuildNotFound(guildId);

        var isOwnSystem = target.Id == ctx.System?.Id;

        var noNameSetMessage = $"{(isOwnSystem ? "Your" : "This")} system does not have a name specific to this server.";
        if (isOwnSystem)
            noNameSetMessage += $" Type `{ctx.DefaultPrefix}system servername <name>` to set one.";

        var settings = await ctx.Repository.GetSystemGuild(guildId, target.Id);

        if (settings.DisplayName == null)
        {
            await ctx.Reply(noNameSetMessage);
            return;
        }

        switch (format)
        {
            case ReplyFormat.Raw:
                await ctx.Reply($"```\n{settings.DisplayName}\n```");
                return;
            case ReplyFormat.Plaintext:
                var eb = new EmbedBuilder()
                                .Description($"Showing servername for system `{target.DisplayHid(ctx.Config)}`{(guildId == ctx.Guild?.Id ? "" : $" in server \"{guild.Name}\"")}");
                await ctx.Reply(settings.DisplayName, embed: eb.Build());
                return;
            default:
                var clearMessage = $" Type `{ctx.DefaultPrefix}system servername {(ctx.DefaultPrefix == "/" ? $"clear{(guildId == ctx.Guild?.Id ? "" : $" server-id:{guildId}")}" : "-clear")}` to clear it.";
                await ctx.Reply(
                                $"{(isOwnSystem ? "Your" : "This")} system's name for {(guildId == ctx.Guild?.Id ? "this server" : $"server \"{guild.Name}\"")} is currently **{settings.DisplayName}**."
                                + (isOwnSystem ? clearMessage
                                + $" Using {settings.DisplayName.Length}/{Limits.MaxSystemNameLength} characters." : ""));
                return;
        }
    }

    public async Task SetTag(JointContext ctx, string newTag)
    {
        // I don't think this null check will ever actually get hit but I'm too scared to remove it
        if (newTag != null)
            if (newTag.Length > Limits.MaxSystemTagLength)
                throw Errors.StringTooLongError("System tag", newTag.Length, Limits.MaxSystemTagLength);

        await ctx.Repository.UpdateSystem(ctx.System.Id, new SystemPatch { Tag = newTag });

        var replyStr = $"{Emojis.Success} System tag changed (using {newTag.Length}/{Limits.MaxSystemTagLength} characters).";
        if (ctx.Config.NameFormat is null || ctx.Config.NameFormat.Contains("{tag}"))
            replyStr += $"Member names will now have the tag {newTag.AsCode()} when proxied.\n{Emojis.Note}To check or change where your tag appears in your name use the command `{ctx.DefaultPrefix}cfg nameformat`.";
        else
            replyStr += $"\n{Emojis.Warn} You do not have a designated place for a tag in your name format so it **will not be put in proxy names**. To change this type `{ctx.DefaultPrefix}cfg nameformat`.";
        if (ctx.Guild != null)
        {
            var guildSettings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, ctx.System.Id);

            if (guildSettings.Tag is not null)
                replyStr += $"\n{Emojis.Note} Note that you have a server tag set ({guildSettings.Tag}) and it will be shown in proxies instead.";
            if (!guildSettings.TagEnabled)
                replyStr += $"\n{Emojis.Note} Note that your tag is disabled in this server and will not be shown in proxies. To change this type `{ctx.DefaultPrefix}system servertag enable{(ctx.DefaultPrefix == "/" ? ":True" : "")}`.";
            if (guildSettings.NameFormat is not null && !guildSettings.NameFormat.Contains("{tag}"))
                replyStr += $"\n{Emojis.Note} You do not have a designated place for a tag in your server name format so it **will not be put in proxy names**. To change this type `{ctx.DefaultPrefix}cfg {(ctx.DefaultPrefix == "/" ? "nameformat server-specific:True" : "server name format")}`.";
        }

        await ctx.Reply(replyStr);
    }
    public async Task ClearTag(JointContext ctx)
    {
        await ctx.Repository.UpdateSystem(ctx.System.Id, new SystemPatch { Tag = null });

        var replyStr = $"{Emojis.Success} System tag cleared.";

        if (ctx.Guild != null)
        {
            var servertag = (await ctx.Repository.GetSystemGuild(ctx.Guild.Id, ctx.System.Id)).Tag;
            if (servertag is not null)
                replyStr += $"\n{Emojis.Note} You have a server tag set in this server ({servertag}) so it will still be shown on proxies.";

            else if (ctx.GuildConfig.RequireSystemTag)
                replyStr += $"\n{Emojis.Warn} This server requires a tag in order to proxy. If you do not add a new tag you will not be able to proxy in this server.";
        }

        await ctx.Reply(replyStr);
    }
    public async Task ShowTag(JointContext ctx, PKSystem target, ReplyFormat format)
    {
        var isOwnSystem = ctx.System?.Id == target.Id;

        var noTagSetMessage = isOwnSystem
            ? $"You currently have no system tag set. To set one, type `{ctx.DefaultPrefix}system tag {(ctx.DefaultPrefix == "/" ? "set tag:" : "")}<tag>`."
            : "This system currently has no system tag set.";

        if (target.Tag == null)
        {
            await ctx.Reply(noTagSetMessage);
            return;
        }

        switch (format)
        {
            case ReplyFormat.Raw:
                await ctx.Reply($"```\n{target.Tag}\n```");
                return;
            case ReplyFormat.Plaintext:
                var eb = new EmbedBuilder()
                    .Description($"Showing tag for system `{target.DisplayHid(ctx.Config)}`");
                await ctx.Reply(target.Tag, embed: eb.Build());
                return;
            default:
                await ctx.Reply($"{(isOwnSystem ? "Your" : "This system's")} current system tag is {target.Tag.AsCode()}."
                + (isOwnSystem ? $"To change it, type `{ctx.DefaultPrefix}system tag {(ctx.DefaultPrefix == "/" ? "set tag:" : "")}<tag>`. To clear it, type `{ctx.DefaultPrefix}system tag {(ctx.DefaultPrefix == "/" ? "" : "-")}clear`." : ""));
                return;
        }
    }

    public async Task SetDescription(JointContext ctx, string newDescription)
    {
        if (newDescription.Length > Limits.MaxDescriptionLength)
            throw Errors.StringTooLongError("Description", newDescription.Length, Limits.MaxDescriptionLength);

        await ctx.Repository.UpdateSystem(ctx.System.Id, new SystemPatch { Description = newDescription });

        await ctx.Reply($"{Emojis.Success} System description changed (using {newDescription.Length}/{Limits.MaxDescriptionLength} characters).");
    }
    public async Task ClearDescription(JointContext ctx)
    {
        await ctx.Repository.UpdateSystem(ctx.System.Id, new SystemPatch { Description = null });

        await ctx.Reply($"{Emojis.Success} System description cleared.");
    }
    public async Task ShowDescription(JointContext ctx, PKSystem target, ReplyFormat format)
    {
        ctx.CheckSystemPrivacy(target.Id, target.DescriptionPrivacy);

        var isOwnSystem = target.Id == ctx.System?.Id;

        var noDescriptionSetMessage = "This system does not have a description set.";
        if (isOwnSystem)
            noDescriptionSetMessage += $" To set one, type `{ctx.DefaultPrefix}s description <description>`.";

        if (target.Description == null)
        {
            await ctx.Reply(noDescriptionSetMessage);
            return;
        }

        switch (format)
        {
            case ReplyFormat.Raw:
                await ctx.Reply($"```\n{target.Description}\n```");
                return;
            case ReplyFormat.Plaintext:
                var eb = new EmbedBuilder()
                    .Description($"Showing description for system `{target.DisplayHid(ctx.Config)}`");
                await ctx.Reply(target.Description, embed: eb.Build());
                return;
            default:
                await ctx.Reply(embed: new EmbedBuilder()
                .Title("System description")
                .Description(target.Description)
                .Footer(new Embed.EmbedFooter(
                    $"To print the description with formatting, type `{ctx.DefaultPrefix}s description -raw`."
                        + (isOwnSystem ? $" To clear it, type `{ctx.DefaultPrefix}s description -clear`. To change it, type `{ctx.DefaultPrefix}s description <new description>`."
                        + $" Using {target.Description.Length}/{Limits.MaxDescriptionLength} characters." : "")))
                .Build());
                return;
        }
    }

}