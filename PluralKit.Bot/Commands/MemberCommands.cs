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
            var memberCount = await _data.GetSystemMemberCount(ctx.System, true);
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
        
        public async Task MemberRandom(Context ctx)
        {
            ctx.CheckSystem();

            var randGen = new System.Random(); 
            //Maybe move this somewhere else in the file structure since it doesn't need to get created at every command

            // TODO: don't buffer these, find something else to do ig
            var members = await _data.GetSystemMembers(ctx.System).Where(m => m.MemberPrivacy == PrivacyLevel.Public).ToListAsync();
            if (members == null || !members.Any())
                throw Errors.NoMembersError;
            var randInt = randGen.Next(members.Count);
            await ctx.Reply(embed: await _embeds.CreateMemberEmbed(ctx.System, members[randInt], ctx.Guild, ctx.LookupContextFor(ctx.System)));

        }

        public async Task RenameMember(Context ctx, PKMember target) {
            // TODO: this method is pretty much a 1:1 copy/paste of the above creation method, find a way to clean?
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            
            var newName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a new name for the member.");

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

            if (ctx.Guild != null)
            {
                var memberGuildConfig = await _data.GetMemberGuildSettings(target, ctx.Guild.Id);
                if (memberGuildConfig.DisplayName != null)
                    await ctx.Reply($"{Emojis.Note} Note that this member has a server name set ({memberGuildConfig.DisplayName.SanitizeMentions()}) in this server ({ctx.Guild.Name.SanitizeMentions()}), and will be proxied using that name here.");
            }

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
            
            async Task<bool> WarnOnConflict(ProxyTag newTag)
            {
                var conflicts = (await _data.GetConflictingProxies(ctx.System, newTag))
                    .Where(m => m.Id != target.Id)
                    .ToList();

                if (conflicts.Count <= 0) return true;

                var conflictList = conflicts.Select(m => $"- **{m.Name}**");
                var msg = await ctx.Reply(
                    $"{Emojis.Warn} The following members have conflicting proxy tags:\n{string.Join('\n', conflictList)}\nDo you want to proceed anyway?");
                return await ctx.PromptYesNo(msg);
            }
            
            // "Sub"command: no arguments clearing
            if (!ctx.HasNext())
            {
                // If we already have multiple tags, this would clear everything, so prompt that
                if (target.ProxyTags.Count > 1)
                {
                    var msg = await ctx.Reply(
                        $"{Emojis.Warn} You already have multiple proxy tags set: {target.ProxyTagsString()}\nDo you want to clear them all?");
                    if (!await ctx.PromptYesNo(msg))
                        throw Errors.GenericCancelled();
                }
                
                target.ProxyTags = new ProxyTag[] { };
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Proxy tags cleared.");
            }
            // Subcommand: "add"
            else if (ctx.Match("add"))
            {
                if (!ctx.HasNext()) throw new PKSyntaxError("You must pass an example proxy to add (eg. `[text]` or `J:text`).");
                
                var tagToAdd = ParseProxyTags(ctx.RemainderOrNull());
                if (target.ProxyTags.Contains(tagToAdd))
                    throw Errors.ProxyTagAlreadyExists(tagToAdd, target);
                
                if (!await WarnOnConflict(tagToAdd))
                    throw Errors.GenericCancelled();

                // It's not guaranteed the list's mutable, so we force it to be
                target.ProxyTags = target.ProxyTags.ToList();
                target.ProxyTags.Add(tagToAdd);
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Added proxy tags `{tagToAdd.ProxyString.SanitizeMentions()}`.");
            }
            // Subcommand: "remove"
            else if (ctx.Match("remove"))
            {
                if (!ctx.HasNext()) throw new PKSyntaxError("You must pass a proxy tag to remove (eg. `[text]` or `J:text`).");

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
                if (!ctx.HasNext()) throw new PKSyntaxError("You must pass an example proxy to set (eg. `[text]` or `J:text`).");

                var requestedTag = ParseProxyTags(ctx.RemainderOrNull());
                
                // This is mostly a legacy command, so it's gonna error out if there's
                // already more than one proxy tag.
                if (target.ProxyTags.Count > 1)
                    throw Errors.LegacyAlreadyHasProxyTag(requestedTag, target);
                
                if (!await WarnOnConflict(requestedTag))
                    throw Errors.GenericCancelled();

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

        public async Task MemberDisplayName(Context ctx, PKMember target)
        {            
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            var newDisplayName = ctx.RemainderOrNull();

            target.DisplayName = newDisplayName;
            await _data.SaveMember(target);

            var successStr = $"{Emojis.Success} ";
            if (newDisplayName != null)
                successStr += $"Member display name changed. This member will now be proxied using the name \"{newDisplayName.SanitizeMentions()}\".";
            else
                successStr += $"Member display name cleared. This member will now be proxied using their member name \"{target.Name.SanitizeMentions()}\".";

            if (ctx.Guild != null)
            {
                var memberGuildConfig = await _data.GetMemberGuildSettings(target, ctx.Guild.Id);
                if (memberGuildConfig.DisplayName != null)
                    successStr += $" However, this member has a server name set in this server ({ctx.Guild.Name.SanitizeMentions()}), and will be proxied using that name, \"{memberGuildConfig.DisplayName.SanitizeMentions()}\", here.";
            }

            await ctx.Reply(successStr);
            
            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }

        public async Task MemberServerName(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            
            // TODO: allow setting server names for different servers/in DMs by ID
            ctx.CheckGuildContext();
            
            var newServerName = ctx.RemainderOrNull();

            var guildSettings = await _data.GetMemberGuildSettings(target, ctx.Guild.Id);
            guildSettings.DisplayName = newServerName;
            await _data.SetMemberGuildSettings(target, ctx.Guild.Id, guildSettings);

            var successStr = $"{Emojis.Success} ";
            if (newServerName != null)
                successStr += $"Member server name changed. This member will now be proxied using the name \"{newServerName.SanitizeMentions()}\" in this server ({ctx.Guild.Name.SanitizeMentions()}).";
            else if (target.DisplayName != null)
                successStr += $"Member server name cleared. This member will now be proxied using their global display name \"{target.DisplayName.SanitizeMentions()}\" in this server ({ctx.Guild.Name.SanitizeMentions()}).";
            else
                successStr += $"Member server name cleared. This member will now be proxied using their member name \"{target.Name.SanitizeMentions()}\" in this server ({ctx.Guild.Name.SanitizeMentions()}).";

            await ctx.Reply(successStr);
            
            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }
        
        public async Task MemberKeepProxy(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            bool newValue;
            if (ctx.Match("on", "enabled", "true", "yes")) newValue = true;
            else if (ctx.Match("off", "disabled", "false", "no")) newValue = false;
            else if (ctx.HasNext()) throw new PKSyntaxError("You must pass either \"on\" or \"off\".");
            else newValue = !target.KeepProxy;

            target.KeepProxy = newValue;
            await _data.SaveMember(target);
            
            if (newValue)
                await ctx.Reply($"{Emojis.Success} Member proxy tags will now be included in the resulting message when proxying.");
            else
                await ctx.Reply($"{Emojis.Success} Member proxy tags will now not be included in the resulting message when proxying.");
            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }

        public async Task MemberPrivacy(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            bool newValue;
            if (ctx.Match("private", "hide", "hidden", "on", "enable", "yes")) newValue = true;
            else if (ctx.Match("public", "show", "shown", "displayed", "off", "disable", "no")) newValue = false;
            else if (ctx.HasNext()) throw new PKSyntaxError("You must pass either \"private\" or \"public\".");
            else newValue = target.MemberPrivacy != PrivacyLevel.Private;

            target.MemberPrivacy = newValue ? PrivacyLevel.Private : PrivacyLevel.Public;
            await _data.SaveMember(target);

            if (newValue)
                await ctx.Reply($"{Emojis.Success} Member privacy set to **private**. This member will no longer show up in member lists and will return limited information when queried by other accounts.");
            else
                await ctx.Reply($"{Emojis.Success} Member privacy set to **public**. This member will now show up in member lists and will return all information when queried by other accounts.");
        }

        public async Task ViewMember(Context ctx, PKMember target)
        {
            var system = await _data.GetSystemById(target.System);
            await ctx.Reply(embed: await _embeds.CreateMemberEmbed(system, target, ctx.Guild, ctx.LookupContextFor(system)));
        }
    }
}