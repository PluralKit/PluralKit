using System.Linq;
using System.Threading.Tasks;
using Discord;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class LinkCommands
    {
        private SystemStore _systems;

        public LinkCommands(SystemStore systems)
        {
            _systems = systems;
        }
        
        public async Task LinkSystem(Context ctx)
        {
            ctx.CheckSystem();
            
            var account = await ctx.MatchUser() ?? throw new PKSyntaxError("You must pass an account to link with (either ID or @mention).");
            var accountIds = await _systems.GetLinkedAccountIds(ctx.System);
            if (accountIds.Contains(account.Id)) throw Errors.AccountAlreadyLinked;

            var existingAccount = await _systems.GetByAccount(account.Id);
            if (existingAccount != null) throw Errors.AccountInOtherSystem(existingAccount); 

            var msg = await ctx.Reply($"{account.Mention}, please confirm the link by clicking the {Emojis.Success} reaction on this message.");
            if (!await ctx.PromptYesNo(msg, user: account)) throw Errors.MemberLinkCancelled;
            await _systems.Link(ctx.System, account.Id);
            await ctx.Reply($"{Emojis.Success} Account linked to system.");
        }

        public async Task UnlinkAccount(Context ctx)
        {
            ctx.CheckSystem();
            
            IUser account;
            if (!ctx.HasNext())
                account = ctx.Author;
            else           
                account = await ctx.MatchUser() ?? throw new PKSyntaxError("You must pass an account to link with (either ID or @mention).");

            var accountIds = (await _systems.GetLinkedAccountIds(ctx.System)).ToList();
            if (!accountIds.Contains(account.Id)) throw Errors.AccountNotLinked;
            if (accountIds.Count == 1) throw Errors.UnlinkingLastAccount;
            
            var msg = await ctx.Reply(
                $"Are you sure you want to unlink {account.Mention} from your system?");
            if (!await ctx.PromptYesNo(msg)) throw Errors.MemberUnlinkCancelled;

            await _systems.Unlink(ctx.System, account.Id);
            await ctx.Reply($"{Emojis.Success} Account unlinked.");
        }
    }
}