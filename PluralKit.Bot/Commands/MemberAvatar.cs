#nullable enable
using System;
using System.Threading.Tasks;

using DSharpPlus.Entities;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class MemberAvatar
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;

        public MemberAvatar(IDatabase db, ModelRepository repo)
        {
            _db = db;
            _repo = repo;
        }
        
        private async Task AvatarClear(AvatarLocation location, Context ctx, PKMember target, MemberGuildSettings? mgs)
        {
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
                    await ctx.Reply($"{Emojis.Success} Member avatar cleared. Note that this member has a server-specific avatar set here, type `pk;member {target.Reference()} serveravatar clear` if you wish to clear that too.");
                else 
                    await ctx.Reply($"{Emojis.Success} Member avatar cleared.");
            }
        }

        private async Task AvatarShow(AvatarLocation location, Context ctx, PKMember target, MemberGuildSettings? guildData)
        {
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
                    throw new PKError($"This member does not have a server avatar set. Type `pk;member {target.Reference()} avatar` to see their global avatar.");
            }
            
            var field = location == AvatarLocation.Server ? $"server avatar (for {ctx.Guild.Name})" : "avatar";
            var cmd = location == AvatarLocation.Server ? "serveravatar" : "avatar";
            
            var eb = new DiscordEmbedBuilder()
                .WithTitle($"{target.NameFor(ctx)}'s {field}")
                .WithImageUrl(currentValue);
            if (target.System == ctx.System?.Id)
                eb.WithDescription($"To clear, use `pk;member {target.Reference()} {cmd} clear`.");
            await ctx.Reply(embed: eb.Build());
        }

        public async Task ServerAvatar(Context ctx, PKMember target)
        {
            ctx.CheckGuildContext();
            var guildData = await _db.Execute(c => _repo.GetMemberGuild(c, ctx.Guild.Id, target.Id));
            await AvatarCommandTree(AvatarLocation.Server, ctx, target, guildData);
        }
        
        public async Task Avatar(Context ctx, PKMember target)
        {
            var guildData = ctx.Guild != null ?
                await _db.Execute(c => _repo.GetMemberGuild(c, ctx.Guild.Id, target.Id))
                : null;

            await AvatarCommandTree(AvatarLocation.Member, ctx, target, guildData);
        }

        private async Task AvatarCommandTree(AvatarLocation location, Context ctx, PKMember target, MemberGuildSettings? guildData)
        {
            // First, see if we need to *clear*
            if (await ctx.MatchClear("this member's avatar"))
            {
                ctx.CheckSystem().CheckOwnMember(target);
                await AvatarClear(location, ctx, target, guildData);
                return;
            }

            // Then, parse an image from the command (from various sources...)
            var avatarArg = await ctx.MatchImage();
            if (avatarArg == null)
            {
                // If we didn't get any, just show the current avatar
                await AvatarShow(location, ctx, target, guildData);
                return;
            }

            ctx.CheckSystem().CheckOwnMember(target);
            await ValidateUrl(avatarArg.Value.Url);
            await UpdateAvatar(location, ctx, target, avatarArg.Value.Url);
            await PrintResponse(location, ctx, target, avatarArg.Value, guildData);
        }

        private static Task ValidateUrl(string url)
        {
            if (url.Length > Limits.MaxUriLength)
                throw Errors.InvalidUrl(url);
            return AvatarUtils.VerifyAvatarOrThrow(url);
        }

        private Task PrintResponse(AvatarLocation location, Context ctx, PKMember target, ParsedImage avatar,
                                   MemberGuildSettings? targetGuildData)
        {
            var typeFrag = location switch
            {
                AvatarLocation.Server => "server avatar",
                AvatarLocation.Member => "avatar",
                _ => throw new ArgumentOutOfRangeException(nameof(location))
            };
            
            var serverFrag = location switch
            {
                AvatarLocation.Server => $" This avatar will now be used when proxying in this server (**{ctx.Guild.Name}**).",
                AvatarLocation.Member when targetGuildData?.AvatarUrl != null => $"\n{Emojis.Note} Note that this member *also* has a server-specific avatar set in this server (**{ctx.Guild.Name}**), and thus changing the global avatar will have no effect here.",
                _ => ""
            };

            var msg = avatar.Source switch
            {
                AvatarSource.User => $"{Emojis.Success} Member {typeFrag} changed to {avatar.SourceUser?.Username}'s avatar!{serverFrag}\n{Emojis.Warn} If {avatar.SourceUser?.Username} changes their avatar, the member's avatar will need to be re-set.",
                AvatarSource.Url => $"{Emojis.Success} Member {typeFrag} changed to the image at the given URL.{serverFrag}",
                AvatarSource.Attachment => $"{Emojis.Success} Member {typeFrag} changed to attached image.{serverFrag}\n{Emojis.Warn} If you delete the message containing the attachment, the avatar will stop working.",
                _ => throw new ArgumentOutOfRangeException()
            };

            // The attachment's already right there, no need to preview it.
            var hasEmbed = avatar.Source != AvatarSource.Attachment;
            return hasEmbed 
                ? ctx.Reply(msg, embed: new DiscordEmbedBuilder().WithImageUrl(avatar.Url).Build()) 
                : ctx.Reply(msg);
        }

        private Task UpdateAvatar(AvatarLocation location, Context ctx, PKMember target, string? url)
        {
            switch (location)
            {
                case AvatarLocation.Server:
                    var serverPatch = new MemberGuildPatch { AvatarUrl = url };
                    return _db.Execute(c => _repo.UpsertMemberGuild(c, target.Id, ctx.Guild.Id, serverPatch));
                case AvatarLocation.Member:
                    var memberPatch = new MemberPatch { AvatarUrl = url };
                    return _db.Execute(c => _repo.UpdateMember(c, target.Id, memberPatch));
                default:
                    throw new ArgumentOutOfRangeException($"Unknown avatar location {location}");
            }
        }

        private enum AvatarLocation
        {
            Member,
            Server
        }
    }
}