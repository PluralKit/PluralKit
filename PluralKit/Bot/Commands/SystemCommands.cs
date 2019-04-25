using System;
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

        private RuntimeResult NO_SYSTEM_ERROR => PKResult.Error($"You do not have a system registered with PluralKit. To create one, type `pk;system new`. If you already have a system registered on another account, type `pk;link {Context.User.Mention}` from that account to link it here.");
        private RuntimeResult OTHER_SYSTEM_CONTEXT_ERROR => PKResult.Error("You can only run this command on your own system.");

        [Command]
        public async Task<RuntimeResult> Query(PKSystem system = null) {
            if (system == null) system = Context.SenderSystem;
            if (system == null) return NO_SYSTEM_ERROR;

            await Context.Channel.SendMessageAsync(embed: await EmbedService.CreateSystemEmbed(system));
            return PKResult.Success();
        }

        [Command("new")]
        public async Task<RuntimeResult> New([Remainder] string systemName = null)
        {
            if (ContextEntity != null) return OTHER_SYSTEM_CONTEXT_ERROR;
            if (Context.SenderSystem != null) return PKResult.Error("You already have a system registered with PluralKit. To view it, type `pk;system`. If you'd like to delete your system and start anew, type `pk;system delete`, or if you'd like to unlink this account from it, type `pk;unlink.");

            var system = await Systems.Create(systemName);
            await Systems.Link(system, Context.User.Id);

            await ReplyAsync("Your system has been created. Type `pk;system` to view it, and type `pk;help` for more information about commands you can use now.");
            return PKResult.Success();
        }

        [Command("name")]
        public async Task<RuntimeResult> Name([Remainder] string newSystemName = null) {
            if (ContextEntity != null) return OTHER_SYSTEM_CONTEXT_ERROR;
            if (Context.SenderSystem == null) return NO_SYSTEM_ERROR;
            if (newSystemName != null && newSystemName.Length > 250) return PKResult.Error($"Your chosen system name is too long. ({newSystemName.Length} > 250 characters)");

            Context.SenderSystem.Name = newSystemName;
            await Systems.Save(Context.SenderSystem);
            return PKResult.Success();
        }

        [Command("description")]
        public async Task<RuntimeResult> Description([Remainder] string newDescription = null) {
            if (ContextEntity != null) return OTHER_SYSTEM_CONTEXT_ERROR;
            if (Context.SenderSystem == null) return NO_SYSTEM_ERROR;
            if (newDescription != null && newDescription.Length > 1000) return PKResult.Error($"Your chosen description is too long. ({newDescription.Length} > 250 characters)");

            Context.SenderSystem.Description = newDescription;
            await Systems.Save(Context.SenderSystem);
            return PKResult.Success("uwu");
        }

        [Command("tag")]
        public async Task<RuntimeResult> Tag([Remainder] string newTag = null) {
            if (ContextEntity != null) return OTHER_SYSTEM_CONTEXT_ERROR;
            if (Context.SenderSystem == null) return NO_SYSTEM_ERROR;

            Context.SenderSystem.Tag = newTag;

            var unproxyableMembers = await Members.GetUnproxyableMembers(Context.SenderSystem);
            //if (unproxyableMembers.Count > 0) {
                throw new Exception("sdjsdflsdf");
            //}

            await Systems.Save(Context.SenderSystem);
            return PKResult.Success("uwu");
        }

        public override async Task<PKSystem> ReadContextParameterAsync(string value)
        {
            var res = await new PKSystemTypeReader().ReadAsync(Context, value, _services);
            return res.IsSuccess ? res.BestMatch as PKSystem : null;
        }
    }
}