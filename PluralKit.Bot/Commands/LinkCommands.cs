using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace PluralKit.Bot.Commands
{
    public class LinkCommands: ModuleBase<PKCommandContext>
    {
        public SystemStore Systems { get; set; }


        [Command("link")]
        [Remarks("link <account>")]
        [MustHaveSystem]
        public async Task LinkSystem(IUser account)
        {
            var accountIds = await Systems.GetLinkedAccountIds(Context.SenderSystem);
            if (accountIds.Contains(account.Id)) throw Errors.AccountAlreadyLinked;

            var existingAccount = await Systems.GetByAccount(account.Id);
            if (existingAccount != null) throw Errors.AccountInOtherSystem(existingAccount); 

            var msg = await Context.Channel.SendMessageAsync(
                $"{account.Mention}, please confirm the link by clicking the {Emojis.Success} reaction on this message.");
            if (!await Context.PromptYesNo(msg, user: account)) throw Errors.MemberLinkCancelled;
            await Systems.Link(Context.SenderSystem, account.Id);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Account linked to system.");
        }

        [Command("unlink")]
        [Remarks("unlink [account]")]
        [MustHaveSystem]
        public async Task UnlinkAccount(IUser account = null)
        {
            if (account == null) account = Context.User;
            
            var accountIds = (await Systems.GetLinkedAccountIds(Context.SenderSystem)).ToList();
            if (!accountIds.Contains(account.Id)) throw Errors.AccountNotLinked;
            if (accountIds.Count == 1) throw Errors.UnlinkingLastAccount;
            
            var msg = await Context.Channel.SendMessageAsync(
                $"Are you sure you want to unlink {account.Mention} from your system?");
            if (!await Context.PromptYesNo(msg)) throw Errors.MemberUnlinkCancelled;

            await Systems.Unlink(Context.SenderSystem, account.Id);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Account unlinked.");
        }
    }
}