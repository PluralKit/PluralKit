using System.Linq;
using System.Threading.Tasks;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class SystemLink
    {
        private IDataStore _data;

        public SystemLink(IDataStore data)
        {
            _data = data;
        }
        
        public async Task LinkSystem(Context ctx)
        {
            ctx.CheckSystem();
            
            var account = await ctx.MatchUser() ?? throw new PKSyntaxError("You must pass an account to link with (either ID or @mention).");
            var accountIds = await _data.GetSystemAccounts(ctx.System);
            if (accountIds.Contains(account.Id)) throw Errors.AccountAlreadyLinked;

            var existingAccount = await _data.GetSystemByAccount(account.Id);
            if (existingAccount != null) throw Errors.AccountInOtherSystem(existingAccount); 

            var msg = await ctx.Reply($"{account.Mention}, please confirm the link by clicking the {Emojis.Success} reaction on this message.");
            if (!await ctx.PromptYesNo(msg, user: account)) throw Errors.MemberLinkCancelled;
            await _data.AddAccount(ctx.System, account.Id);
            await ctx.Reply($"{Emojis.Success} Account linked to system.");
        }

        public async Task UnlinkAccount(Context ctx)
        {
            ctx.CheckSystem();
            
            ulong id;
            if (!ctx.HasNext())
                id = ctx.Author.Id;
            else if (!ctx.MatchUserRaw(out id))
                throw new PKSyntaxError("You must pass an account to link with (either ID or @mention).");

            var accountIds = (await _data.GetSystemAccounts(ctx.System)).ToList();
            if (!accountIds.Contains(id)) throw Errors.AccountNotLinked;
            if (accountIds.Count == 1) throw Errors.UnlinkingLastAccount;
            
            var msg = await ctx.Reply(
                $"Are you sure you want to unlink <@{id}> from your system?");
            if (!await ctx.PromptYesNo(msg)) throw Errors.MemberUnlinkCancelled;

            await _data.RemoveAccount(ctx.System, id);
            await ctx.Reply($"{Emojis.Success} Account unlinked.");
        }
    }
}