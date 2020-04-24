using System.Linq;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class MemberAvatar
    {
        private IDataStore _data;

        public MemberAvatar(IDataStore data)
        {
            _data = data;
        }

        public async Task Avatar(Context ctx, PKMember target)
        {
            var guildData = ctx.Guild != null ? await _data.GetMemberGuildSettings(target, ctx.Guild.Id) : null;

            if (ctx.Match("clear", "remove", "reset") || ctx.MatchFlag("c", "clear"))
            {
                if (ctx.System == null) throw Errors.NoSystemError;
                if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
                
                target.AvatarUrl = null;
                await _data.SaveMember(target);
                
                if (guildData?.AvatarUrl != null)
                    await ctx.Reply($"{Emojis.Success} Member avatar cleared. Note that this member has a server-specific avatar set here, type `pk;member {target.Hid} serveravatar clear` if you wish to clear that too.");
                else 
                    await ctx.Reply($"{Emojis.Success} Member avatar cleared.");
                return;
            }
            
            if (ctx.RemainderOrNull() == null && ctx.Message.Attachments.Count == 0)
            {
                if ((target.AvatarUrl?.Trim() ?? "").Length > 0)
                {
                    var eb = new DiscordEmbedBuilder()
                        .WithTitle($"{target.Name.SanitizeMentions()}'s avatar")
                        .WithImageUrl(target.AvatarUrl);
                    if (target.System == ctx.System?.Id)
                        eb.WithDescription($"To clear, use `pk;member {target.Hid} avatar -clear`.");
                    await ctx.Reply(embed: eb.Build());
                }
                else
                {
                    if (target.System == ctx.System?.Id)
                        throw new PKSyntaxError($"This member does not have an avatar set. Set one by attaching an image to this command, or by passing an image URL or @mention.");
                    throw new PKError($"This member does not have an avatar set.");
                }

                return;
            }
            var user = await ctx.MatchUser();
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            else if (user != null)
            {
                if (user.AvatarUrl == user.DefaultAvatarUrl) throw Errors.UserHasNoAvatar; //TODO: is this necessary?
                target.AvatarUrl = user.GetAvatarUrl(ImageFormat.Png, size: 256);
                
                await _data.SaveMember(target);
            
                var embed = new DiscordEmbedBuilder().WithImageUrl(target.AvatarUrl).Build();
                await ctx.Reply(
                    $"{Emojis.Success} Member avatar changed to {user.Username}'s avatar! {Emojis.Warn} Please note that if {user.Username} changes their avatar, the member's avatar will need to be re-set.", embed: embed);
            }
            else if (ctx.RemainderOrNull() is string url)
            {
                await AvatarUtils.VerifyAvatarOrThrow(url);
                target.AvatarUrl = url;
                await _data.SaveMember(target);

                var embed = new DiscordEmbedBuilder().WithImageUrl(url).Build();
                await ctx.Reply($"{Emojis.Success} Member avatar changed.", embed: embed);
            }
            else if (ctx.Message.Attachments.FirstOrDefault() is DiscordAttachment attachment)
            {
                await AvatarUtils.VerifyAvatarOrThrow(attachment.Url);
                target.AvatarUrl = attachment.Url;
                await _data.SaveMember(target);

                await ctx.Reply($"{Emojis.Success} Member avatar changed to attached image. Please note that if you delete the message containing the attachment, the avatar will stop working.");
            }
            // No-arguments no-attachment case covered by conditional at the very top
        }
        
        public async Task ServerAvatar(Context ctx, PKMember target)
        {
            ctx.CheckGuildContext();
            var guildData = await _data.GetMemberGuildSettings(target, ctx.Guild.Id);
            
            if (ctx.Match("clear", "remove", "reset") || ctx.MatchFlag("c", "clear"))
            {
                if (ctx.System == null) throw Errors.NoSystemError;
                if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
                
                guildData.AvatarUrl = null;
                await _data.SetMemberGuildSettings(target, ctx.Guild.Id, guildData);
                
                if (target.AvatarUrl != null)
                    await ctx.Reply($"{Emojis.Success} Member server avatar cleared. This member will now use the global avatar in this server (**{ctx.Guild.Name}**).");
                else
                    await ctx.Reply($"{Emojis.Success} Member server avatar cleared. This member now has no avatar.");
            }
            
            if (ctx.RemainderOrNull() == null && ctx.Message.Attachments.Count == 0)
            {
                if ((guildData.AvatarUrl?.Trim() ?? "").Length > 0)
                {
                    var eb = new DiscordEmbedBuilder()
                        .WithTitle($"{target.Name.SanitizeMentions()}'s server avatar (for {ctx.Guild.Name})")
                        .WithImageUrl(guildData.AvatarUrl);
                    if (target.System == ctx.System?.Id)
                        eb.WithDescription($"To clear, use `pk;member {target.Hid} serveravatar clear`.");
                    await ctx.Reply(embed: eb.Build());
                }
                else
                    throw new PKError($"This member does not have a server avatar set. Type `pk;member {target.Hid} avatar` to see their global avatar.");

                return;
            }
            var user = await ctx.MatchUser();
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            if (user != null)
            {
                if (user.AvatarUrl == user.DefaultAvatarUrl) throw Errors.UserHasNoAvatar;
                guildData.AvatarUrl = user.GetAvatarUrl(ImageFormat.Png, size: 256);
                await _data.SetMemberGuildSettings(target, ctx.Guild.Id, guildData);
            
                var embed = new DiscordEmbedBuilder().WithImageUrl(guildData.AvatarUrl).Build();
                await ctx.Reply(
                    $"{Emojis.Success} Member server avatar changed to {user.Username}'s avatar! This avatar will now be used when proxying in this server (**{ctx.Guild.Name}**). {Emojis.Warn} Please note that if {user.Username} changes their avatar, the member's server avatar will need to be re-set.", embed: embed);
            }
            else if (ctx.RemainderOrNull() is string url)
            {
                await AvatarUtils.VerifyAvatarOrThrow(url);
                guildData.AvatarUrl = url;
                await _data.SetMemberGuildSettings(target, ctx.Guild.Id, guildData);

                var embed = new DiscordEmbedBuilder().WithImageUrl(url).Build();
                await ctx.Reply($"{Emojis.Success} Member server avatar changed. This avatar will now be used when proxying in this server (**{ctx.Guild.Name}**).", embed: embed);
            }
            else if (ctx.Message.Attachments.FirstOrDefault() is DiscordAttachment attachment)
            {
                await AvatarUtils.VerifyAvatarOrThrow(attachment.Url);
                guildData.AvatarUrl = attachment.Url;
                await _data.SetMemberGuildSettings(target, ctx.Guild.Id, guildData);

                await ctx.Reply($"{Emojis.Success} Member server avatar changed to attached image. This avatar will now be used when proxying in this server (**{ctx.Guild.Name}**). Please note that if you delete the message containing the attachment, the avatar will stop working.");
            }
            // No-arguments no-attachment case covered by conditional at the very top
        }
    }
}