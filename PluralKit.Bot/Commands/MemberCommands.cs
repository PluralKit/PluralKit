using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Commands;
using NodaTime;

namespace PluralKit.Bot.Commands
{
    [Group("member")]
    public class MemberCommands : ContextParameterModuleBase<PKMember>
    {
        public SystemStore Systems { get; set; }
        public MemberStore Members { get; set; }
        public EmbedService Embeds { get; set;  }

        public override string Prefix => "member";
        public override string ContextNoun => "member";

        [Command("new")]
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
                var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.Name}\" (with ID `{existingMember.Hid}`). Do you want to create another member with the same name?");
                if (!await Context.PromptYesNo(msg)) throw new PKError("Member creation cancelled.");
            }

            // Create the member
            var member = await Members.Create(Context.SenderSystem, memberName);
            
            // Send confirmation and space hint
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member \"{memberName}\" (`{member.Hid}`) registered! Type `pk;help member` for a list of commands to edit this member.");
            if (memberName.Contains(" ")) await Context.Channel.SendMessageAsync($"{Emojis.Note} Note that this member's name contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it.");
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
                var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.Name}\" (`{existingMember.Hid}`). Do you want to rename this member to that name too?");
                if (!await Context.PromptYesNo(msg)) throw new PKError("Member renaming cancelled.");
            }

            // Rename the member
            ContextEntity.Name = newName;
            await Members.Save(ContextEntity);

            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member renamed.");
            if (newName.Contains(" ")) await Context.Channel.SendMessageAsync($"{Emojis.Note} Note that this member's name now contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it.");
        }

        [Command("description")]
        [Alias("info", "bio", "text")]
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
                if (!Regex.IsMatch(color, "[0-9a-f]{6}")) throw Errors.InvalidColorError(color);
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

        [Command]
        [Alias("view", "show", "info")]
        [Remarks("member")]
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