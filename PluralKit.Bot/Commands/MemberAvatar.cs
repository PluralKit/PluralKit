#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using DSharpPlus;
using DSharpPlus.Entities;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class MemberAvatar
    {
        private readonly IDatabase _db;

        public MemberAvatar(IDatabase db)
        {
            _db = db;
        }
        
        private async Task AvatarClear(AvatarLocation location, Context ctx, PKMember target, MemberGuildSettings? mgs)
        {
            ctx.CheckSystem().CheckOwnMember(target);
            await UpdateAvatar(location, ctx, target, null);
            if (location == AvatarLocation.Server)
            {
                if (target.AvatarUrl != null)
                    await ctx.Reply($"{Emojis.Success} Member server avatar cleared. This member will now use the global avatar in this server (**{ctx.Guild.Name}**).");
                else
                    await ctx.Reply($"{Emojis.Success} Member server avatar cleared. This member now has no avatar.");
            }
            else
            {
                if (mgs?.AvatarUrl != null)
                    await ctx.Reply($"{Emojis.Success} Member avatar cleared. Note that this member has a server-specific avatar set here, type `pk;member {target.Hid} serveravatar clear` if you wish to clear that too.");
                else 
                    await ctx.Reply($"{Emojis.Success} Member avatar cleared.");
            }
        }

        private async Task AvatarShow(AvatarLocation location, Context ctx, PKMember target, MemberGuildSettings? guildData)
        {
            var field = location == AvatarLocation.Server ? $"server avatar (for {ctx.Guild.Name})" : "avatar";
            var cmd = location == AvatarLocation.Server ? "serveravatar" : "avatar";
            
            var currentValue = location == AvatarLocation.Member ? target.AvatarUrl : guildData?.AvatarUrl;
            var canAccess = location != AvatarLocation.Member || target.AvatarPrivacy.CanAccess(ctx.LookupContextFor(target));
            if (string.IsNullOrEmpty(currentValue) || !canAccess)
            {
                if (location == AvatarLocation.Member)
                {
                    if (target.System == ctx.System?.Id)
                        throw new PKSyntaxError("This member does not have an avatar set. Set one by attaching an image to this command, or by passing an image URL or @mention.");
                    throw new PKError("This member does not have an avatar set.");
                }

                if (location == AvatarLocation.Server)
                    throw new PKError($"This member does not have a server avatar set. Type `pk;member {target.Hid} avatar` to see their global avatar.");
            }

            var eb = new DiscordEmbedBuilder()
                .WithTitle($"{target.NameFor(ctx).SanitizeMentions()}'s {field}")
                .WithImageUrl(currentValue);
            if (target.System == ctx.System?.Id)
                eb.WithDescription($"To clear, use `pk;member {target.Hid} {cmd} clear`.");
            await ctx.Reply(embed: eb.Build());
        }

        private async Task AvatarFromUser(AvatarLocation location, Context ctx, PKMember target, DiscordUser user)
        {
            ctx.CheckSystem().CheckOwnMember(target);
            if (user.AvatarHash == null) throw Errors.UserHasNoAvatar;
            
            var url = user.GetAvatarUrl(ImageFormat.Png, 256);
            await UpdateAvatar(location, ctx, target, url);
            
            var embed = new DiscordEmbedBuilder().WithImageUrl(url).Build();
            if (location == AvatarLocation.Server)
                await ctx.Reply($"{Emojis.Success} Member server avatar changed to {user.Username}'s avatar! This avatar will now be used when proxying in this server (**{ctx.Guild.Name}**). {Emojis.Warn} Please note that if {user.Username} changes their avatar, the member's server avatar will need to be re-set.", embed: embed);
            else if (location == AvatarLocation.Member)
                await ctx.Reply($"{Emojis.Success} Member avatar changed to {user.Username}'s avatar! {Emojis.Warn} Please note that if {user.Username} changes their avatar, the member's avatar will need to be re-set.", embed: embed);
        }

        private async Task AvatarFromArg(AvatarLocation location, Context ctx, PKMember target, string url)
        {
            ctx.CheckSystem().CheckOwnMember(target);
            if (url.Length > Limits.MaxUriLength) throw Errors.InvalidUrl(url);
            await AvatarUtils.VerifyAvatarOrThrow(url);

            await UpdateAvatar(location, ctx, target, url);

            var embed = new DiscordEmbedBuilder().WithImageUrl(url).Build();
            if (location == AvatarLocation.Server)
                await ctx.Reply($"{Emojis.Success} Member server avatar changed. This avatar will now be used when proxying in this server (**{ctx.Guild.Name}**).", embed: embed);
        }

        private async Task AvatarFromAttachment(AvatarLocation location, Context ctx, PKMember target, DiscordAttachment attachment)
        {
            ctx.CheckSystem().CheckOwnMember(target);
            await AvatarUtils.VerifyAvatarOrThrow(attachment.Url);
            await UpdateAvatar(location, ctx, target, attachment.Url);
            if (location == AvatarLocation.Server)
                await ctx.Reply($"{Emojis.Success} Member server avatar changed to attached image. This avatar will now be used when proxying in this server (**{ctx.Guild.Name}**). Please note that if you delete the message containing the attachment, the avatar will stop working.");
            else if (location == AvatarLocation.Member)
                await ctx.Reply($"{Emojis.Success} Member avatar changed to attached image. Please note that if you delete the message containing the attachment, the avatar will stop working.");
        }

        public async Task ServerAvatar(Context ctx, PKMember target)
        {
            ctx.CheckGuildContext();
            var guildData = await _db.Execute(c => c.QueryOrInsertMemberGuildConfig(ctx.Guild.Id, target.Id));
            await AvatarCommandTree(AvatarLocation.Server, ctx, target, guildData);
        }
        
        public async Task Avatar(Context ctx, PKMember target)
        {
            var guildData = ctx.Guild != null ?
                await _db.Execute(c => c.QueryOrInsertMemberGuildConfig(ctx.Guild.Id, target.Id))
                : null;

            await AvatarCommandTree(AvatarLocation.Member, ctx, target, guildData);
        }

        private async Task AvatarCommandTree(AvatarLocation location, Context ctx, PKMember target, MemberGuildSettings? guildData)
        {
            if (ctx.Match("clear", "remove", "reset") || ctx.MatchFlag("c", "clear"))
                await AvatarClear(location, ctx, target, guildData);
            else if (ctx.RemainderOrNull() == null && ctx.Message.Attachments.Count == 0)
                await AvatarShow(location, ctx, target, guildData);
            else if (await ctx.MatchUser() is {} user)
                await AvatarFromUser(location, ctx, target, user);
            else if (ctx.RemainderOrNull() is {} url)
                await AvatarFromArg(location, ctx, target, url);
            else if (ctx.Message.Attachments.FirstOrDefault() is {} attachment)
                await AvatarFromAttachment(location, ctx, target, attachment);
            else throw new Exception("Unexpected condition when parsing avatar command");
        }

        private Task UpdateAvatar(AvatarLocation location, Context ctx, PKMember target, string? avatar) =>
            location switch
            {
                AvatarLocation.Server => _db.Execute(c =>
                    c.ExecuteAsync(
                        "update member_guild set avatar_url = @Avatar where member = @Member and guild = @Guild",
                        new {Avatar = avatar, Guild = ctx.Guild.Id, Member = target.Id})),
                AvatarLocation.Member => _db.Execute(c =>
                    c.ExecuteAsync(
                        "update members set avatar_url = @Avatar where id = @Member",
                        new {Avatar = avatar, Member = target.Id})),
                _ => throw new ArgumentOutOfRangeException($"Unknown avatar location {location}")
            };

        private enum AvatarLocation
        {
            Member,
            Server
        }
    }
}