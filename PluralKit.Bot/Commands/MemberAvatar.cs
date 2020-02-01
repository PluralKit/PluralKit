using System.Linq;
using System.Threading.Tasks;

using Discord;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class MemberAvatar
    {
        private IDataStore _data;
        private ProxyCacheService _proxyCache;

        public MemberAvatar(IDataStore data, ProxyCacheService proxyCache)
        {
            _data = data;
            _proxyCache = proxyCache;
        }

        public async Task Avatar(Context ctx, PKMember target)
        {
            if (ctx.RemainderOrNull() == null && ctx.Message.Attachments.Count == 0)
            {
                if ((target.AvatarUrl?.Trim() ?? "").Length > 0)
                {
                    var eb = new EmbedBuilder()
                        .WithTitle($"{target.Name.SanitizeMentions()}'s avatar")
                        .WithImageUrl(target.AvatarUrl);
                    if (target.System == ctx.System?.Id)
                        eb.WithDescription($"To clear, use `pk;member {target.Hid} avatar clear`.");
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
            
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            if (ctx.Match("clear", "remove"))
            {
                target.AvatarUrl = null;
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Member avatar cleared.");
            }
            else if (await ctx.MatchUser() is IUser user)
            {
                if (user.AvatarId == null) throw Errors.UserHasNoAvatar;
                target.AvatarUrl = user.GetAvatarUrl(ImageFormat.Png, size: 256);
                
                await _data.SaveMember(target);
            
                var embed = new EmbedBuilder().WithImageUrl(target.AvatarUrl).Build();
                await ctx.Reply(
                    $"{Emojis.Success} Member avatar changed to {user.Username}'s avatar! {Emojis.Warn} Please note that if {user.Username} changes their avatar, the webhook's avatar will need to be re-set.", embed: embed);

            }
            else if (ctx.RemainderOrNull() is string url)
            {
                await Utils.VerifyAvatarOrThrow(url);
                target.AvatarUrl = url;
                await _data.SaveMember(target);

                var embed = new EmbedBuilder().WithImageUrl(url).Build();
                await ctx.Reply($"{Emojis.Success} Member avatar changed.", embed: embed);
            }
            else if (ctx.Message.Attachments.FirstOrDefault() is Attachment attachment)
            {
                await Utils.VerifyAvatarOrThrow(attachment.Url);
                target.AvatarUrl = attachment.Url;
                await _data.SaveMember(target);

                await ctx.Reply($"{Emojis.Success} Member avatar changed to attached image. Please note that if you delete the message containing the attachment, the avatar will stop working.");
            }
            // No-arguments no-attachment case covered by conditional at the very top

            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }
    }
}