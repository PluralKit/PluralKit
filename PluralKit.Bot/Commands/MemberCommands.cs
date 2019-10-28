using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using NodaTime;

using PluralKit.Bot.CommandSystem;
using PluralKit.Core;

namespace PluralKit.Bot.Commands
{
    public class MemberCommands
    {
        private IDataStore _data;
        private EmbedService _embeds;

        private ProxyCacheService _proxyCache;

        public MemberCommands(IDataStore data, EmbedService embeds, ProxyCacheService proxyCache)
        {
            _data = data;
            _embeds = embeds;
            _proxyCache = proxyCache;
        }

        public async Task NewMember(Context ctx) {
            if (ctx.System == null) throw Errors.NoSystemError;
            var memberName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a member name.");
            
            // Hard name length cap
            if (memberName.Length > Limits.MaxMemberNameLength) throw Errors.MemberNameTooLongError(memberName.Length);

            // Warn if there's already a member by this name
            var existingMember = await _data.GetMemberByName(ctx.System, memberName);
            if (existingMember != null) {
                var msg = await ctx.Reply($"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.Name.SanitizeMentions()}\" (with ID `{existingMember.Hid}`). Do you want to create another member with the same name?");
                if (!await ctx.PromptYesNo(msg)) throw new PKError("Member creation cancelled.");
            }

            // Enforce per-system member limit
            var memberCount = await _data.GetSystemMemberCount(ctx.System);
            if (memberCount >= Limits.MaxMemberCount)
                throw Errors.MemberLimitReachedError;

            // Create the member
            var member = await _data.CreateMember(ctx.System, memberName);
            memberCount++;
            
            // Send confirmation and space hint
            await ctx.Reply($"{Emojis.Success} Member \"{memberName.SanitizeMentions()}\" (`{member.Hid}`) registered! See the user guide for commands for editing this member: https://pluralkit.me/guide#member-management");
            if (memberName.Contains(" "))
                await ctx.Reply($"{Emojis.Note} Note that this member's name contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it, or just use the member's 5-character ID (which is `{member.Hid}`).");
            if (memberCount >= Limits.MaxMemberCount)
                await ctx.Reply($"{Emojis.Warn} You have reached the per-system member limit ({Limits.MaxMemberCount}). You will be unable to create additional members until existing members are deleted.");
            else if (memberCount >= Limits.MaxMembersWarnThreshold)
                await ctx.Reply($"{Emojis.Warn} You are approaching the per-system member limit ({memberCount} / {Limits.MaxMemberCount} members). Please review your member list for unused or duplicate members.");

            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }
        
        public async Task RenameMember(Context ctx, PKMember target) {
            // TODO: this method is pretty much a 1:1 copy/paste of the above creation method, find a way to clean?
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            
            var newName = ctx.RemainderOrNull();

            // Hard name length cap
            if (newName.Length > Limits.MaxMemberNameLength) throw Errors.MemberNameTooLongError(newName.Length);

            // Warn if there's already a member by this name
            var existingMember = await _data.GetMemberByName(ctx.System, newName);
            if (existingMember != null) {
                var msg = await ctx.Reply($"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.Name.SanitizeMentions()}\" (`{existingMember.Hid}`). Do you want to rename this member to that name too?");
                if (!await ctx.PromptYesNo(msg)) throw new PKError("Member renaming cancelled.");
            }

            // Rename the member
            target.Name = newName;
            await _data.SaveMember(target);

            await ctx.Reply($"{Emojis.Success} Member renamed.");
            if (newName.Contains(" ")) await ctx.Reply($"{Emojis.Note} Note that this member's name now contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it.");
            if (target.DisplayName != null) await ctx.Reply($"{Emojis.Note} Note that this member has a display name set ({target.DisplayName.SanitizeMentions()}), and will be proxied using that name instead.");
            
            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }
        
        public async Task MemberDescription(Context ctx, PKMember target) {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            var description = ctx.RemainderOrNull();
            if (description.IsLongerThan(Limits.MaxDescriptionLength)) throw Errors.DescriptionTooLongError(description.Length);

            target.Description = description;
            await _data.SaveMember(target);

            await ctx.Reply($"{Emojis.Success} Member description {(description == null ? "cleared" : "changed")}.");
        }
        
        public async Task MemberPronouns(Context ctx, PKMember target) {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            var pronouns = ctx.RemainderOrNull();
            if (pronouns.IsLongerThan(Limits.MaxPronounsLength)) throw Errors.MemberPronounsTooLongError(pronouns.Length);

            target.Pronouns = pronouns;
            await _data.SaveMember(target);

            await ctx.Reply($"{Emojis.Success} Member pronouns {(pronouns == null ? "cleared" : "changed")}.");
        }

        public async Task MemberColor(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            var color = ctx.RemainderOrNull();
            if (color != null)
            {
                if (color.StartsWith("#")) color = color.Substring(1);
                if (!Regex.IsMatch(color, "^[0-9a-fA-F]{6}$")) throw Errors.InvalidColorError(color);
            }

            target.Color = color;
            await _data.SaveMember(target);

            await ctx.Reply($"{Emojis.Success} Member color {(color == null ? "cleared" : "changed")}.");
        }

        public async Task MemberBirthday(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            
            LocalDate? date = null;
            var birthday = ctx.RemainderOrNull();
            if (birthday != null)
            {
                date = PluralKit.Utils.ParseDate(birthday, true);
                if (date == null) throw Errors.BirthdayParseError(birthday);
            }

            target.Birthday = date;
            await _data.SaveMember(target);
            
            await ctx.Reply($"{Emojis.Success} Member birthdate {(date == null ? "cleared" : $"changed to {target.BirthdayString}")}.");
        }

        public async Task MemberProxy(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            ProxyTag ParseProxyTags(string exampleProxy)
            {
                // // Make sure there's one and only one instance of "text" in the example proxy given
                var prefixAndSuffix = exampleProxy.Split("text");
                if (prefixAndSuffix.Length < 2) throw Errors.ProxyMustHaveText;
                if (prefixAndSuffix.Length > 2) throw Errors.ProxyMultipleText;
                return new ProxyTag(prefixAndSuffix[0], prefixAndSuffix[1]);
            }
            
            // "Sub"command: no arguments clearing
            if (!ctx.HasNext())
            {
                // If we already have multiple tags, this would clear everything, so prompt that
                var msg = await ctx.Reply(
                    $"{Emojis.Warn} You already have multiple proxy tags set: {target.ProxyTagsString()}\nDo you want to clear them all?");
                if (!await ctx.PromptYesNo(msg))
                    throw Errors.GenericCancelled();

                target.ProxyTags = new ProxyTag[] { };
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Proxy tags cleared.");
            }
            // Subcommand: "add"
            else if (ctx.Match("add"))
            {
                var tagToAdd = ParseProxyTags(ctx.RemainderOrNull());
                if (target.ProxyTags.Contains(tagToAdd))
                    throw Errors.ProxyTagAlreadyExists(tagToAdd, target);
                
                // It's not guaranteed the list's mutable, so we force it to be
                target.ProxyTags = target.ProxyTags.ToList();
                target.ProxyTags.Add(tagToAdd);
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Added proxy tags `{tagToAdd.ProxyString.SanitizeMentions()}`.");
            }
            // Subcommand: "remove"
            else if (ctx.Match("remove"))
            {
                var tagToRemove = ParseProxyTags(ctx.RemainderOrNull());
                if (!target.ProxyTags.Contains(tagToRemove))
                    throw Errors.ProxyTagDoesNotExist(tagToRemove, target);

                // It's not guaranteed the list's mutable, so we force it to be
                target.ProxyTags = target.ProxyTags.ToList();
                target.ProxyTags.Remove(tagToRemove);
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Removed proxy tags `{tagToRemove.ProxyString.SanitizeMentions()}`.");
            }
            // Subcommand: bare proxy tag given
            else
            {
                var requestedTag = ParseProxyTags(ctx.RemainderOrNull());
                
                // This is mostly a legacy command, so it's gonna error out if there's
                // already more than one proxy tag.
                if (target.ProxyTags.Count > 1)
                    throw Errors.LegacyAlreadyHasProxyTag(requestedTag, target);

                target.ProxyTags = new[] {requestedTag};
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Member proxy tags set to `{requestedTag.ProxyString.SanitizeMentions()}`.");
            }

            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }

        public async Task MemberDelete(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            
            await ctx.Reply($"{Emojis.Warn} Are you sure you want to delete \"{target.Name.SanitizeMentions()}\"? If so, reply to this message with the member's ID (`{target.Hid}`). __***This cannot be undone!***__");
            if (!await ctx.ConfirmWithReply(target.Hid)) throw Errors.MemberDeleteCancelled;
            await _data.DeleteMember(target);
            await ctx.Reply($"{Emojis.Success} Member deleted.");
            
            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }
        
        public async Task MemberAvatar(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            
            if (await ctx.MatchUser() is IUser user)
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
            else
            {
                target.AvatarUrl = null;
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Member avatar cleared.");
            }
            
            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }

        public async Task MemberDisplayName(Context ctx, PKMember target)
        {            
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            var newDisplayName = ctx.RemainderOrNull();

            target.DisplayName = newDisplayName;
            await _data.SaveMember(target);

            var successStr = $"{Emojis.Success} ";
            if (newDisplayName != null)
            {
                successStr +=
                    $"Member display name changed. This member will now be proxied using the name \"{newDisplayName.SanitizeMentions()}\".";
            }
            else
            {
                successStr += $"Member display name cleared. This member will now be proxied using their member name \"{target.Name.SanitizeMentions()}\".";
            }
            await ctx.Reply(successStr);
            
            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }
        
        public async Task ViewMember(Context ctx, PKMember target)
        {
            var system = await _data.GetSystemById(target.System);
            await ctx.Reply(embed: await _embeds.CreateMemberEmbed(system, target));
        }
    }
}