using Dapper;

using PluralKit.Core;

namespace PluralKit.Bot;

public class MemberProxy
{
    public async Task Proxy(Context ctx, PKMember target)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        ProxyTag ParseProxyTags(string exampleProxy)
        {
            // // Make sure there's one and only one instance of "text" in the example proxy given
            var prefixAndSuffix = exampleProxy.Split("text");
            if (prefixAndSuffix.Length == 1) prefixAndSuffix = prefixAndSuffix[0].Split("TEXT");
            if (prefixAndSuffix.Length < 2) throw Errors.ProxyMustHaveText;
            if (prefixAndSuffix.Length > 2) throw Errors.ProxyMultipleText;
            return new ProxyTag(prefixAndSuffix[0], prefixAndSuffix[1]);
        }

        async Task<bool> WarnOnConflict(ProxyTag newTag)
        {
            var query = "select * from (select *, (unnest(proxy_tags)).prefix as prefix, (unnest(proxy_tags)).suffix as suffix from members where system = @System) as _ where prefix is not distinct from @Prefix and suffix is not distinct from @Suffix and id != @Existing";
            var conflicts = (await ctx.Database.Execute(conn => conn.QueryAsync<PKMember>(query,
                new { newTag.Prefix, newTag.Suffix, Existing = target.Id, system = target.System }))).ToList();

            if (conflicts.Count <= 0) return true;

            var conflictList = conflicts.Select(m => $"- **{m.NameFor(ctx)}**");
            var msg = $"{Emojis.Warn} The following members have conflicting proxy tags:\n{string.Join('\n', conflictList)}\nDo you want to proceed anyway?";
            return await ctx.PromptYesNo(msg, "Proceed");
        }

        // "Sub"command: clear flag
        if (await ctx.MatchClear())
        {
            // If we already have multiple tags, this would clear everything, so prompt that
            if (target.ProxyTags.Count > 1)
            {
                var msg = $"{Emojis.Warn} You already have multiple proxy tags set: {target.ProxyTagsString()}\nDo you want to clear them all?";
                if (!await ctx.PromptYesNo(msg, "Clear"))
                    throw Errors.GenericCancelled();
            }

            var patch = new MemberPatch { ProxyTags = Partial<ProxyTag[]>.Present(new ProxyTag[0]) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Proxy tags cleared.");
        }
        // "Sub"command: no arguments; will print proxy tags
        else if (!ctx.HasNext(false))
        {
            if (target.ProxyTags.Count == 0)
                await ctx.Reply("This member does not have any proxy tags.");
            else
                await ctx.Reply($"This member's proxy tags are:\n{target.ProxyTagsString("\n")}");
        }
        // Subcommand: "add"
        else if (ctx.Match("add", "append"))
        {
            if (!ctx.HasNext(false))
                throw new PKSyntaxError("You must pass an example proxy to add (eg. `[text]` or `J:text`).");

            var tagToAdd = ParseProxyTags(ctx.RemainderOrNull(false));
            if (tagToAdd.IsEmpty) throw Errors.EmptyProxyTags(target);
            if (target.ProxyTags.Contains(tagToAdd))
                throw Errors.ProxyTagAlreadyExists(tagToAdd, target);
            if (tagToAdd.ProxyString.Length > Limits.MaxProxyTagLength)
                throw new PKError(
                    $"Proxy tag too long ({tagToAdd.ProxyString.Length} > {Limits.MaxProxyTagLength} characters).");

            if (!await WarnOnConflict(tagToAdd))
                throw Errors.GenericCancelled();

            var newTags = target.ProxyTags.ToList();
            newTags.Add(tagToAdd);
            var patch = new MemberPatch { ProxyTags = Partial<ProxyTag[]>.Present(newTags.ToArray()) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Added proxy tags {tagToAdd.ProxyString.AsCode()}.");
        }
        // Subcommand: "remove"
        else if (ctx.Match("remove", "delete"))
        {
            if (!ctx.HasNext(false))
                throw new PKSyntaxError("You must pass a proxy tag to remove (eg. `[text]` or `J:text`).");

            var tagToRemove = ParseProxyTags(ctx.RemainderOrNull(false));
            if (tagToRemove.IsEmpty) throw Errors.EmptyProxyTags(target);
            if (!target.ProxyTags.Contains(tagToRemove))
                throw Errors.ProxyTagDoesNotExist(tagToRemove, target);

            var newTags = target.ProxyTags.ToList();
            newTags.Remove(tagToRemove);
            var patch = new MemberPatch { ProxyTags = Partial<ProxyTag[]>.Present(newTags.ToArray()) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Removed proxy tags {tagToRemove.ProxyString.AsCode()}.");
        }
        // Subcommand: bare proxy tag given
        else
        {
            var requestedTag = ParseProxyTags(ctx.RemainderOrNull(false));
            if (requestedTag.IsEmpty) throw Errors.EmptyProxyTags(target);

            // This is mostly a legacy command, so it's gonna warn if there's
            // already more than one proxy tag.
            if (target.ProxyTags.Count > 1)
            {
                var msg = $"This member already has more than one proxy tag set: {target.ProxyTagsString()}\nDo you want to replace them?";
                if (!await ctx.PromptYesNo(msg, "Replace"))
                    throw Errors.GenericCancelled();
            }

            if (!await WarnOnConflict(requestedTag))
                throw Errors.GenericCancelled();

            var newTags = new[] { requestedTag };
            var patch = new MemberPatch { ProxyTags = Partial<ProxyTag[]>.Present(newTags) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Member proxy tags set to {requestedTag.ProxyString.AsCode()}.");
        }
    }
}