using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using NodaTime;
using PluralKit.Core;
using Image = SixLabors.ImageSharp.Image;

namespace PluralKit.Bot.Commands
{
    [Group("member")]
    [Alias("m")]
    public class MemberCommands : ContextParameterModuleBase<PKMember>
    {
        public SystemStore Systems { get; set; }
        public MemberStore Members { get; set; }
        public EmbedService Embeds { get; set;  }

        public override string Prefix => "member";
        public override string ContextNoun => "member";

        [Command("new")]
        [Alias("n", "add", "create", "register")]
        [Remarks("member new <name>")]
        [MustHaveSystem]
        public async Task NewMember([Remainder] string memberName) {
            // Hard name length cap
            if (memberName.Length > Limits.MaxMemberNameLength) throw Errors.MemberNameTooLongError(memberName.Length);

            // Warn if member name will be unproxyable (with/without tag)
            if (memberName.Length > Context.SenderSystem.MaxMemberNameLength) {
                var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} Member name too long ({memberName.Length} > {Context.SenderSystem.MaxMemberNameLength} characters), this member will be unproxyable. Do you want to create it anyway? (You can change the name later)");
                if (!await Context.PromptYesNo(msg)) throw new PKError("Member creation cancelled.");
            }

            // Warn if there's already a member by this name
            var existingMember = await Members.GetByName(Context.SenderSystem, memberName);
            if (existingMember != null) {
                var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.Name.Sanitize()}\" (with ID `{existingMember.Hid}`). Do you want to create another member with the same name?");
                if (!await Context.PromptYesNo(msg)) throw new PKError("Member creation cancelled.");
            }

            // Create the member
            var member = await Members.Create(Context.SenderSystem, memberName);
            
            // Send confirmation and space hint
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member \"{memberName.Sanitize()}\" (`{member.Hid}`) registered! Type `pk;help member` for a list of commands to edit this member.");
            if (memberName.Contains(" ")) await Context.Channel.SendMessageAsync($"{Emojis.Note} Note that this member's name contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it, or just use the member's 5-character ID (which is `{member.Hid}`).");
        }

        [Command("rename")]
        [Alias("name", "changename", "setname")]
        [Remarks("member <member> rename <newname>")]
        [MustPassOwnMember]
        public async Task RenameMember([Remainder] string newName) {
            // TODO: this method is pretty much a 1:1 copy/paste of the above creation method, find a way to clean?

            // Hard name length cap
            if (newName.Length > Limits.MaxMemberNameLength) throw Errors.MemberNameTooLongError(newName.Length);

            // Warn if member name will be unproxyable (with/without tag)
            if (newName.Length > Context.SenderSystem.MaxMemberNameLength) {
                var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} New member name too long ({newName.Length} > {Context.SenderSystem.MaxMemberNameLength} characters), this member will be unproxyable. Do you want to change it anyway?");
                if (!await Context.PromptYesNo(msg)) throw new PKError("Member renaming cancelled.");
            }

            // Warn if there's already a member by this name
            var existingMember = await Members.GetByName(Context.SenderSystem, newName);
            if (existingMember != null) {
                var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.Name.Sanitize()}\" (`{existingMember.Hid}`). Do you want to rename this member to that name too?");
                if (!await Context.PromptYesNo(msg)) throw new PKError("Member renaming cancelled.");
            }

            // Rename the member
            ContextEntity.Name = newName;
            await Members.Save(ContextEntity);

            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member renamed.");
            if (newName.Contains(" ")) await Context.Channel.SendMessageAsync($"{Emojis.Note} Note that this member's name now contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it.");
        }

        [Command("description")]
        [Alias("info", "bio", "text", "desc")]
        [Remarks("member <member> description <description>")]
        [MustPassOwnMember]
        public async Task MemberDescription([Remainder] string description = null) {
            if (description.IsLongerThan(Limits.MaxDescriptionLength)) throw Errors.DescriptionTooLongError(description.Length);

            ContextEntity.Description = description;
            await Members.Save(ContextEntity);

            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member description {(description == null ? "cleared" : "changed")}.");
        }

        [Command("pronouns")]
        [Alias("pronoun")]
        [Remarks("member <member> pronouns <pronouns>")]
        [MustPassOwnMember]
        public async Task MemberPronouns([Remainder] string pronouns = null) {
            if (pronouns.IsLongerThan(Limits.MaxPronounsLength)) throw Errors.MemberPronounsTooLongError(pronouns.Length);

            ContextEntity.Pronouns = pronouns;
            await Members.Save(ContextEntity);

            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member pronouns {(pronouns == null ? "cleared" : "changed")}.");
        }

        [Command("color")]
        [Alias("colour")]
        [Remarks("member <member> color <color>")]
        [MustPassOwnMember]
        public async Task MemberColor([Remainder] string color = null)
        {
            if (color != null)
            {
                if (color.StartsWith("#")) color = color.Substring(1);
                if (!Regex.IsMatch(color, "^[0-9a-f]{6}$")) throw Errors.InvalidColorError(color);
            }

            ContextEntity.Color = color;
            await Members.Save(ContextEntity);

            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member color {(color == null ? "cleared" : "changed")}.");
        }

        [Command("birthday")]
        [Alias("birthdate", "bday", "cakeday", "bdate")]
        [Remarks("member <member> birthday <birthday>")]
        [MustPassOwnMember]
        public async Task MemberBirthday([Remainder] string birthday = null)
        {
            LocalDate? date = null;
            if (birthday != null)
            {
                date = PluralKit.Utils.ParseDate(birthday, true);
                if (date == null) throw Errors.BirthdayParseError(birthday);
            }

            ContextEntity.Birthday = date;
            await Members.Save(ContextEntity);
            
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member birthdate {(date == null ? "cleared" : $"changed to {ContextEntity.BirthdayString}")}.");
        }

        [Command("proxy")]
        [Alias("proxy", "tags", "proxytags", "brackets")]
        [Remarks("member <member> proxy <proxy tags>")]
        [MustPassOwnMember]
        public async Task MemberProxy([Remainder] string exampleProxy = null)
        {
            // Handling the clear case in an if here to keep the body dedented
            if (exampleProxy == null)
            {
                // Just reset and send OK message
                ContextEntity.Prefix = null;
                ContextEntity.Suffix = null;
                await Members.Save(ContextEntity);
                await Context.Channel.SendMessageAsync($"{Emojis.Success} Member proxy tags cleared.");
                return;
            }
            
            // Make sure there's one and only one instance of "text" in the example proxy given
            var prefixAndSuffix = exampleProxy.Split("text");
            if (prefixAndSuffix.Length < 2) throw Errors.ProxyMustHaveText;
            if (prefixAndSuffix.Length > 2) throw Errors.ProxyMultipleText;

            // If the prefix/suffix is empty, use "null" instead (for DB)
            ContextEntity.Prefix = prefixAndSuffix[0].Length > 0 ? prefixAndSuffix[0] : null;
            ContextEntity.Suffix = prefixAndSuffix[1].Length > 0 ? prefixAndSuffix[1] : null;
            await Members.Save(ContextEntity);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member proxy tags changed to `{ContextEntity.ProxyString.Sanitize()}`. Try proxying now!");
        }

        [Command("delete")]
        [Alias("remove", "destroy", "erase", "yeet")]
        [Remarks("member <member> delete")]
        [MustPassOwnMember]
        public async Task MemberDelete()
        {
            await Context.Channel.SendMessageAsync($"{Emojis.Warn} Are you sure you want to delete \"{ContextEntity.Name.Sanitize()}\"? If so, reply to this message with the member's ID (`{ContextEntity.Hid}`). __***This cannot be undone!***__");
            if (!await Context.ConfirmWithReply(ContextEntity.Hid)) throw Errors.MemberDeleteCancelled;
            await Members.Delete(ContextEntity);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member deleted.");
        }

        [Command("avatar")]
        [Alias("profile", "picture", "icon", "image", "pic", "pfp")]
        [Remarks("member <member> avatar <avatar url>")]
        [MustPassOwnMember]
        public async Task MemberAvatarByMention(IUser member)
        {
            if (member.AvatarId == null) throw Errors.UserHasNoAvatar;
            ContextEntity.AvatarUrl = member.GetAvatarUrl(ImageFormat.Png, size: 256);
            
            var embed = new EmbedBuilder().WithImageUrl(ContextEntity.AvatarUrl).Build();
            await Context.Channel.SendMessageAsync(
                $"{Emojis.Success} Member avatar changed to {member.Username}'s avatar! {Emojis.Warn} Please note that if {member.Username} changes their avatar, the webhook's avatar will need to be re-set.", embed: embed);
        }

        [Command("avatar")]
        [Alias("profile", "picture", "icon", "image", "pic", "pfp")]
        [Remarks("member <member> avatar <avatar url>")]
        [MustPassOwnMember]
        public async Task MemberAvatar([Remainder] string avatarUrl = null)
        {
            string url = avatarUrl ?? Context.Message.Attachments.FirstOrDefault()?.ProxyUrl;
            if (url != null) await Context.BusyIndicator(() => Utils.VerifyAvatarOrThrow(url));

            ContextEntity.AvatarUrl = url;
            await Members.Save(ContextEntity);

            var embed = url != null ? new EmbedBuilder().WithImageUrl(url).Build() : null;
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member avatar {(url == null ? "cleared" : "changed")}.", embed: embed);
        }

        [Command]
        [Alias("view", "show", "info")]
        [Remarks("member <member>")]
        public async Task ViewMember(PKMember member)
        {
            var system = await Systems.GetById(member.System);
            await Context.Channel.SendMessageAsync(embed: await Embeds.CreateMemberEmbed(system, member));
        }
        
        public override async Task<PKMember> ReadContextParameterAsync(string value)
        {
            var res = await new PKMemberTypeReader().ReadAsync(Context, value, _services);
            return res.IsSuccess ? res.BestMatch as PKMember : null;        
        }
    }
}