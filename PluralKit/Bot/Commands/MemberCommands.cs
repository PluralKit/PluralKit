using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace PluralKit.Bot.Commands
{
    [Group("member")]
    public class MemberCommands : ContextParameterModuleBase<PKMember>
    {
        public MemberStore Members { get; set; }

        public override string Prefix => "member";
        public override string ContextNoun => "member";

        [Command("new")]
        [Remarks("member new <name>")]
        [MustHaveSystem]
        public async Task NewMember([Remainder] string memberName) {
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

            // Rename the mebmer
            ContextEntity.Name = newName;
            await Members.Save(ContextEntity);

            await Context.Channel.SendMessageAsync($"{Emojis.Success} Member renamed.");
            if (newName.Contains(" ")) await Context.Channel.SendMessageAsync($"{Emojis.Note} Note that this member's name now contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it.");
        }

        public override async Task<PKMember> ReadContextParameterAsync(string value)
        {
            var res = await new PKMemberTypeReader().ReadAsync(Context, value, _services);
            return res.IsSuccess ? res.BestMatch as PKMember : null;        
        }
    }
}