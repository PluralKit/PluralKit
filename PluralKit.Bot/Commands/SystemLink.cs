using Myriad.Extensions;

using PluralKit.Core;

namespace PluralKit.Bot;

public class SystemLink
{
    public async Task LinkSystem(Context ctx)
    {
        ctx.CheckSystem();

        var account = await ctx.MatchUser() ??
                      throw new PKSyntaxError("You must pass an account to link with (either ID or @mention).");
        var accountIds = await ctx.Repository.GetSystemAccounts(ctx.System.Id);
        if (accountIds.Contains(account.Id))
            throw Errors.AccountAlreadyLinked;

        var existingAccount = await ctx.Repository.GetSystemByAccount(account.Id);
        if (existingAccount != null)
            throw Errors.AccountInOtherSystem(existingAccount, ctx.Config, ctx.DefaultPrefix);

        var msg = $"{account.Mention()}, please confirm the link.";
        if (!await ctx.PromptYesNo(msg, "Confirm", account, false)) throw Errors.MemberLinkCancelled;
        await ctx.Repository.AddAccount(ctx.System.Id, account.Id);
        await ctx.Reply($"{Emojis.Success} Account linked to system.");
    }

    public async Task UnlinkAccount(Context ctx)
    {
        ctx.CheckSystem();

        ulong id;
        if (!ctx.MatchUserRaw(out id))
            throw new PKSyntaxError("You must pass an account to unlink from (either ID or @mention).");

        var accountIds = (await ctx.Repository.GetSystemAccounts(ctx.System.Id)).ToList();
        if (!accountIds.Contains(id)) throw Errors.AccountNotLinked;
        if (accountIds.Count == 1) throw Errors.UnlinkingLastAccount(ctx.DefaultPrefix);

        var msg = $"Are you sure you want to unlink <@{id}> from your system?";
        if (!await ctx.PromptYesNo(msg, "Unlink")) throw Errors.MemberUnlinkCancelled;

        await ctx.Repository.RemoveAccount(ctx.System.Id, id);
        await ctx.Reply($"{Emojis.Success} Account unlinked.");
    }
}