using System.Linq;
using System.Threading.Tasks;

using DSharpPlus.Entities;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class SystemLink
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;

        public SystemLink(IDatabase db, ModelRepository repo)
        {
            _db = db;
            _repo = repo;
        }
        
        public async Task LinkSystem(Context ctx)
        {
            ctx.CheckSystem();

            await using var conn = await _db.Obtain();
            
            var account = await ctx.MatchUser() ?? throw new PKSyntaxError("You must pass an account to link with (either ID or @mention).");
            var accountIds = await _repo.GetSystemAccounts(conn, ctx.System.Id);
            if (accountIds.Contains(account.Id))
                throw Errors.AccountAlreadyLinked;

            var existingAccount = await _repo.GetSystemByAccount(conn, account.Id);
            if (existingAccount != null)
                throw Errors.AccountInOtherSystem(existingAccount); 

            var msg = $"{account.Mention}, please confirm the link by clicking the {Emojis.Success} reaction on this message.";
            var mentions = new IMention[] { new UserMention(account) };
            if (!await ctx.PromptYesNo(msg, user: account, mentions: mentions, matchFlag: false)) throw Errors.MemberLinkCancelled;
            await _repo.AddAccount(conn, ctx.System.Id, account.Id);
            await ctx.Reply($"{Emojis.Success} Account linked to system.");
        }

        public async Task UnlinkAccount(Context ctx)
        {
            ctx.CheckSystem();
            
            await using var conn = await _db.Obtain();

            ulong id;
            if (!ctx.HasNext())
                id = ctx.Author.Id;
            else if (!ctx.MatchUserRaw(out id))
                throw new PKSyntaxError("You must pass an account to link with (either ID or @mention).");

            var accountIds = (await _repo.GetSystemAccounts(conn, ctx.System.Id)).ToList();
            if (!accountIds.Contains(id)) throw Errors.AccountNotLinked;
            if (accountIds.Count == 1) throw Errors.UnlinkingLastAccount;
            
            var msg = $"Are you sure you want to unlink <@{id}> from your system?";
            if (!await ctx.PromptYesNo(msg)) throw Errors.MemberUnlinkCancelled;

            await _repo.RemoveAccount(conn, ctx.System.Id, id);
            await ctx.Reply($"{Emojis.Success} Account unlinked.");
        }
    }
}