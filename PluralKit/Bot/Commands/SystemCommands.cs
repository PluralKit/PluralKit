using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Dapper;
using Discord.Commands;

namespace PluralKit.Bot.Commands
{
    [Group("system")]
    public class SystemCommands : ContextParameterModuleBase<PKSystem>
    {
        public override string Prefix => "system";
        public SystemStore Systems {get; set;}
        public MemberStore Members {get; set;}
        public EmbedService EmbedService {get; set;}

        private PKError NO_SYSTEM_ERROR => new PKError($"You do not have a system registered with PluralKit. To create one, type `pk;system new`. If you already have a system registered on another account, type `pk;link {Context.User.Mention}` from that account to link it here.");
        private PKError OTHER_SYSTEM_CONTEXT_ERROR => new PKError("You can only run this command on your own system.");

        [Command]
        public async Task Query(PKSystem system = null) {
            if (system == null) system = Context.SenderSystem;
            if (system == null) throw NO_SYSTEM_ERROR;

            await Context.Channel.SendMessageAsync(embed: await EmbedService.CreateSystemEmbed(system));
        }

        [Command("new")]
        public async Task New([Remainder] string systemName = null)
        {
            if (ContextEntity != null) throw OTHER_SYSTEM_CONTEXT_ERROR;
            if (Context.SenderSystem != null) throw new PKError("You already have a system registered with PluralKit. To view it, type `pk;system`. If you'd like to delete your system and start anew, type `pk;system delete`, or if you'd like to unlink this account from it, type `pk;unlink.");

            var system = await Systems.Create(systemName);
            await Systems.Link(system, Context.User.Id);

            await ReplyAsync("Your system has been created. Type `pk;system` to view it, and type `pk;help` for more information about commands you can use now.");
        }

        [Command("name")]
        public async Task Name([Remainder] string newSystemName = null) {
            if (ContextEntity != null) throw OTHER_SYSTEM_CONTEXT_ERROR;
            if (Context.SenderSystem == null) throw NO_SYSTEM_ERROR;
            if (newSystemName != null && newSystemName.Length > 250) throw new PKError($"Your chosen system name is too long. ({newSystemName.Length} > 250 characters)");

            Context.SenderSystem.Name = newSystemName;
            await Systems.Save(Context.SenderSystem);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} System name {(newSystemName != null ? "changed" : "cleared")}.");
        }

        [Command("description")]
        public async Task Description([Remainder] string newDescription = null) {
            if (ContextEntity != null) throw OTHER_SYSTEM_CONTEXT_ERROR;
            if (Context.SenderSystem == null) throw NO_SYSTEM_ERROR;
            if (newDescription != null && newDescription.Length > 1000) throw new PKError($"Your chosen description is too long. ({newDescription.Length} > 250 characters)");

            Context.SenderSystem.Description = newDescription;
            await Systems.Save(Context.SenderSystem);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} System description {(newDescription != null ? "changed" : "cleared")}.");
        }

        [Command("tag")]
        public async Task Tag([Remainder] string newTag = null) {
            if (ContextEntity != null) throw OTHER_SYSTEM_CONTEXT_ERROR;
            if (Context.SenderSystem == null) throw NO_SYSTEM_ERROR;
            if (newTag.Length > 30) throw new PKError($"Your chosen description is too long. ({newTag.Length} > 30 characters)");

            Context.SenderSystem.Tag = newTag;

            // Check unproxyable messages *after* changing the tag (so it's seen in the method) but *before* we save to DB (so we can cancel)
            var unproxyableMembers = await Members.GetUnproxyableMembers(Context.SenderSystem);
            if (unproxyableMembers.Count > 0) {
                var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} Changing your system tag to '{newTag}' will result in the following members being unproxyable, since the tag would bring their name over 32 characters:\n**{string.Join(", ", unproxyableMembers.Select((m) => m.Name))}**\nDo you want to continue anyway?");
                if (!await Context.PromptYesNo(msg, TimeSpan.FromMinutes(5))) throw new PKError("Tag change cancelled.");
            }

            await Systems.Save(Context.SenderSystem);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} System tag {(newTag != null ? "changed" : "cleared")}.");
        }

        public override async Task<PKSystem> ReadContextParameterAsync(string value)
        {
            var res = await new PKSystemTypeReader().ReadAsync(Context, value, _services);
            return res.IsSuccess ? res.BestMatch as PKSystem : null;
        }
    }
}