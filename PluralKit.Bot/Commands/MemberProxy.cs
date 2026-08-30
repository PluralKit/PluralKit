using Dapper;

using PluralKit.Core;

namespace PluralKit.Bot;

public class MemberProxy
{
    public async Task ShowProxy(Context ctx, PKMember target)
    {
        if (target.ProxyTags.Count == 0)
            await ctx.Reply("This member does not have any proxy tags.");
        else
            await ctx.Reply($"This member's proxy tags are:\n{target.ProxyTagsString("\n")}");
    }

    public async Task ClearProxy(Context ctx, PKMember target, bool confirmYes = false)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        // If we already have multiple tags, this would clear everything, so prompt that
        if (target.ProxyTags.Count > 1)
        {
            var msg = $"{Emojis.Warn} You already have multiple proxy tags set: {target.ProxyTagsString()}\nDo you want to clear them all?";
            if (!await ctx.PromptYesNo(msg, "Clear", flagValue: confirmYes))
                throw Errors.GenericCancelled();
        }

        var patch = new MemberPatch { ProxyTags = Partial<ProxyTag[]>.Present(new ProxyTag[0]) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        await ctx.Reply($"{Emojis.Success} Proxy tags cleared.");
    }

    public async Task AddProxy(Context ctx, PKMember target, string proxyString, bool confirmYes = false)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        var tagToAdd = ParseProxyTag(proxyString);
        if (tagToAdd.IsEmpty) throw Errors.EmptyProxyTags(target, ctx);
        if (target.ProxyTags.Contains(tagToAdd))
            throw Errors.ProxyTagAlreadyExists(tagToAdd, target);
        if (tagToAdd.ProxyString.Length > Limits.MaxProxyTagLength)
            throw new PKError(
                $"Proxy tag too long ({tagToAdd.ProxyString.Length} > {Limits.MaxProxyTagLength} characters).");

        if (!await WarnOnConflict(ctx, target, tagToAdd, confirmYes))
            throw Errors.GenericCancelled();

        var newTags = target.ProxyTags.ToList();
        newTags.Add(tagToAdd);
        var patch = new MemberPatch { ProxyTags = Partial<ProxyTag[]>.Present(newTags.ToArray()) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        await ctx.Reply($"{Emojis.Success} Added proxy tags {tagToAdd.ProxyString.AsCode()} (using {tagToAdd.ProxyString.Length}/{Limits.MaxProxyTagLength} characters).");
    }

    public async Task RemoveProxy(Context ctx, PKMember target, string proxyString)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        var tagToRemove = ParseProxyTag(proxyString);
        if (tagToRemove.IsEmpty) throw Errors.EmptyProxyTags(target, ctx);
        if (!target.ProxyTags.Contains(tagToRemove))
            throw Errors.ProxyTagDoesNotExist(tagToRemove, target);

        var newTags = target.ProxyTags.ToList();
        newTags.Remove(tagToRemove);
        var patch = new MemberPatch { ProxyTags = Partial<ProxyTag[]>.Present(newTags.ToArray()) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        await ctx.Reply($"{Emojis.Success} Removed proxy tags {tagToRemove.ProxyString.AsCode()}.");
    }

    public async Task SetProxy(Context ctx, PKMember target, string proxyString, bool confirmYes = false)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        var requestedTag = ParseProxyTag(proxyString);
        if (requestedTag.IsEmpty) throw Errors.EmptyProxyTags(target, ctx);

        if (target.ProxyTags.Count > 1)
        {
            var msg = $"This member already has more than one proxy tag set: {target.ProxyTagsString()}\nDo you want to replace them?";
            if (!await ctx.PromptYesNo(msg, "Replace", flagValue: confirmYes))
                throw Errors.GenericCancelled();
        }

        if (requestedTag.ProxyString.Length > Limits.MaxProxyTagLength)
            throw new PKError(
                $"Proxy tag too long ({requestedTag.ProxyString.Length} > {Limits.MaxProxyTagLength} characters).");

        if (!await WarnOnConflict(ctx, target, requestedTag, confirmYes))
            throw Errors.GenericCancelled();

        var newTags = new[] { requestedTag };
        var patch = new MemberPatch { ProxyTags = Partial<ProxyTag[]>.Present(newTags) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        await ctx.Reply($"{Emojis.Success} Member proxy tags set to {requestedTag.ProxyString.AsCode()} (using {requestedTag.ProxyString.Length}/{Limits.MaxProxyTagLength} characters).");
    }

    private ProxyTag ParseProxyTag(string proxyString)
    {
        // Make sure there's one and only one instance of "text" in the example proxy given
        var prefixAndSuffix = proxyString.Split("text");
        if (prefixAndSuffix.Length == 1) prefixAndSuffix = prefixAndSuffix[0].Split("TEXT");
        if (prefixAndSuffix.Length < 2) throw Errors.ProxyMustHaveText;
        if (prefixAndSuffix.Length > 2) throw Errors.ProxyMultipleText;
        return new ProxyTag(prefixAndSuffix[0], prefixAndSuffix[1]);
    }

    private async Task<bool> WarnOnConflict(Context ctx, PKMember target, ProxyTag newTag, bool confirmYes = false)
    {
        var query = "select * from (select *, (unnest(proxy_tags)).prefix as prefix, (unnest(proxy_tags)).suffix as suffix from members where system = @System) as _ where prefix is not distinct from @Prefix and suffix is not distinct from @Suffix and id != @Existing";
        var conflicts = (await ctx.Database.Execute(conn => conn.QueryAsync<PKMember>(query,
            new { newTag.Prefix, newTag.Suffix, Existing = target.Id, system = target.System }))).ToList();

        if (conflicts.Count <= 0) return true;

        var conflictList = conflicts.Select(m => $"- **{m.NameFor(ctx)}**");
        var msg = $"{Emojis.Warn} The following members have conflicting proxy tags:\n{string.Join('\n', conflictList)}\nDo you want to proceed anyway?";
        return await ctx.PromptYesNo(msg, "Proceed", flagValue: confirmYes);
    }
}