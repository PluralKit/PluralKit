using System.Text.RegularExpressions;
using System.Threading.Tasks;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class MemberEdit
    {
        private IDataStore _data;

        public MemberEdit(IDataStore data)
        {
            _data = data;
        }

        public async Task Name(Context ctx, PKMember target) {
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
        }
        
        public async Task Description(Context ctx, PKMember target) {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            var description = ctx.RemainderOrNull()?.NormalizeLineEndSpacing();
            if (description.IsLongerThan(Limits.MaxDescriptionLength)) throw Errors.DescriptionTooLongError(description.Length);

            target.Description = description;
            await _data.SaveMember(target);

            await ctx.Reply($"{Emojis.Success} Member description {(description == null ? "cleared" : "changed")}.");
        }
        
        public async Task Pronouns(Context ctx, PKMember target) {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            var pronouns = ctx.RemainderOrNull();
            if (pronouns.IsLongerThan(Limits.MaxPronounsLength)) throw Errors.MemberPronounsTooLongError(pronouns.Length);

            target.Pronouns = pronouns;
            await _data.SaveMember(target);

            await ctx.Reply($"{Emojis.Success} Member pronouns {(pronouns == null ? "cleared" : "changed")}.");
        }

        public async Task Color(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            var color = ctx.RemainderOrNull();
            if (color != null)
            {
                if (color.StartsWith("#")) color = color.Substring(1);
                if (!Regex.IsMatch(color, "^[0-9a-fA-F]{6}$")) throw Errors.InvalidColorError(color);
            }

            target.Color = color?.ToLower();
            await _data.SaveMember(target);

            await ctx.Reply($"{Emojis.Success} Member color {(color == null ? "cleared" : "changed")}.");
        }

        public async Task Birthday(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            
            LocalDate? date = null;
            var birthday = ctx.RemainderOrNull();
            if (birthday != null)
            {
                date = DateUtils.ParseDate(birthday, true);
                if (date == null) throw Errors.BirthdayParseError(birthday);
            }

            target.Birthday = date;
            await _data.SaveMember(target);
            
            await ctx.Reply($"{Emojis.Success} Member birthdate {(date == null ? "cleared" : $"changed to {target.BirthdayString}")}.");
        }

        public async Task DisplayName(Context ctx, PKMember target)
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
        }

        public async Task ServerName(Context ctx, PKMember target)
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
        }
        
        public async Task KeepProxy(Context ctx, PKMember target)
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
        }

        public async Task Privacy(Context ctx, PKMember target, PrivacyLevel? newValueFromCommand)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            PrivacyLevel newValue;
            if (ctx.Match("private", "hide", "hidden", "on", "enable", "yes")) newValue = PrivacyLevel.Private;
            else if (ctx.Match("public", "show", "shown", "displayed", "off", "disable", "no")) newValue = PrivacyLevel.Public;
            else if (ctx.HasNext()) throw new PKSyntaxError("You must pass either \"private\" or \"public\".");
            // If we're getting a value from command (eg. "pk;m <name> private" == always private, "pk;m <name> public == always public"), use that instead of parsing/toggling
            else newValue = newValueFromCommand ?? (target.MemberPrivacy != PrivacyLevel.Private ? PrivacyLevel.Private : PrivacyLevel.Public); 

            target.MemberPrivacy = newValue;
            await _data.SaveMember(target);

            if (newValue == PrivacyLevel.Private)
                await ctx.Reply($"{Emojis.Success} Member privacy set to **private**. This member will no longer show up in member lists and will return limited information when queried by other accounts.");
            else
                await ctx.Reply($"{Emojis.Success} Member privacy set to **public**. This member will now show up in member lists and will return all information when queried by other accounts.");
        }
        
        public async Task Delete(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;
            
            await ctx.Reply($"{Emojis.Warn} Are you sure you want to delete \"{target.Name.SanitizeMentions()}\"? If so, reply to this message with the member's ID (`{target.Hid}`). __***This cannot be undone!***__");
            if (!await ctx.ConfirmWithReply(target.Hid)) throw Errors.MemberDeleteCancelled;
            await _data.DeleteMember(target);
            await ctx.Reply($"{Emojis.Success} Member deleted.");
        }
    }
}