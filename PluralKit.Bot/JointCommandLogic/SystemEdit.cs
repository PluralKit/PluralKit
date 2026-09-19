using Myriad.Builders;

using PluralKit.Core;

namespace PluralKit.Bot;

public class SystemEditLogic
{
    public async Task SetTag(JointContext ctx, string newTag)
    {
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

    public async Task ShowTag(JointContext ctx, PKSystem target, ReplyFormat format = ReplyFormat.Standard)
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
}