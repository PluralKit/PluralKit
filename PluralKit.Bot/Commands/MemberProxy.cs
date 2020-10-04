using System.Linq;
using System.Threading.Tasks;

using Dapper;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class MemberProxy
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        
        public MemberProxy(IDatabase db, ModelRepository repo)
        {
            _db = db;
            _repo = repo;
        }

        public async Task Proxy(Context ctx, PKMember target)
        {
            if (ctx.System == null) throw Errors.NoSystemError;
            if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

            ProxyTag ParseProxyTags(string exampleProxy)
            {
                // // Make sure there's one and only one instance of "text" in the example proxy given
                var prefixAndSuffix = exampleProxy.Split("text");
                if (prefixAndSuffix.Length < 2) throw Errors.ProxyMustHaveText;
                if (prefixAndSuffix.Length > 2) throw Errors.ProxyMultipleText;
                return new ProxyTag(prefixAndSuffix[0], prefixAndSuffix[1]);
            }
            
            async Task<bool> WarnOnConflict(ProxyTag newTag)
            {
                var query = "select * from (select *, (unnest(proxy_tags)).prefix as prefix, (unnest(proxy_tags)).suffix as suffix from members where system = @System) as _ where prefix = @Prefix and suffix = @Suffix and id != @Existing";
                var conflicts = (await _db.Execute(conn => conn.QueryAsync<PKMember>(query,
                    new {Prefix = newTag.Prefix, Suffix = newTag.Suffix, Existing = target.Id, system = target.System}))).ToList();
                
                if (conflicts.Count <= 0) return true;

                var conflictList = conflicts.Select(m => $"- **{m.NameFor(ctx)}**");
                var msg = $"{Emojis.Warn} The following members have conflicting proxy tags:\n{string.Join('\n', conflictList)}\nDo you want to proceed anyway?";
                return await ctx.PromptYesNo(msg);
            }
            
            // "Sub"command: clear flag
            if (await ctx.MatchClear())
            {
                // If we already have multiple tags, this would clear everything, so prompt that
                if (target.ProxyTags.Count > 1)
                {
                    var msg = $"{Emojis.Warn} You already have multiple proxy tags set: {target.ProxyTagsString()}\nDo you want to clear them all?";
                    if (!await ctx.PromptYesNo(msg))
                        throw Errors.GenericCancelled();
                }
                
                var patch = new MemberPatch {ProxyTags = Partial<ProxyTag[]>.Present(new ProxyTag[0])};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));
                
                await ctx.Reply($"{Emojis.Success} Proxy tags cleared.");
            }
            // "Sub"command: no arguments; will print proxy tags
            else if (!ctx.HasNext(skipFlags: false))
            {
                if (target.ProxyTags.Count == 0)
                    await ctx.Reply("This member does not have any proxy tags.");
                else
                    await ctx.Reply($"This member's proxy tags are:\n{target.ProxyTagsString("\n")}");
            }
            // Subcommand: "add"
            else if (ctx.Match("add", "append"))
            {
                if (!ctx.HasNext(skipFlags: false)) throw new PKSyntaxError("You must pass an example proxy to add (eg. `[text]` or `J:text`).");
                
                var tagToAdd = ParseProxyTags(ctx.RemainderOrNull(skipFlags: false));
                if (tagToAdd.IsEmpty) throw Errors.EmptyProxyTags(target);
                if (target.ProxyTags.Contains(tagToAdd))
                    throw Errors.ProxyTagAlreadyExists(tagToAdd, target);
                
                if (!await WarnOnConflict(tagToAdd))
                    throw Errors.GenericCancelled();

                var newTags = target.ProxyTags.ToList();
                newTags.Add(tagToAdd);
                var patch = new MemberPatch {ProxyTags = Partial<ProxyTag[]>.Present(newTags.ToArray())};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));

                await ctx.Reply($"{Emojis.Success} Added proxy tags {tagToAdd.ProxyString.AsCode()}.");
            }
            // Subcommand: "remove"
            else if (ctx.Match("remove", "delete"))
            {
                if (!ctx.HasNext(skipFlags: false)) throw new PKSyntaxError("You must pass a proxy tag to remove (eg. `[text]` or `J:text`).");

                var tagToRemove = ParseProxyTags(ctx.RemainderOrNull(skipFlags: false));
                if (tagToRemove.IsEmpty) throw Errors.EmptyProxyTags(target);
                if (!target.ProxyTags.Contains(tagToRemove))
                    throw Errors.ProxyTagDoesNotExist(tagToRemove, target);

                var newTags = target.ProxyTags.ToList();
                newTags.Remove(tagToRemove);
                var patch = new MemberPatch {ProxyTags = Partial<ProxyTag[]>.Present(newTags.ToArray())};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));

                await ctx.Reply($"{Emojis.Success} Removed proxy tags {tagToRemove.ProxyString.AsCode()}.");
            }
            // Subcommand: bare proxy tag given
            else
            {
                var requestedTag = ParseProxyTags(ctx.RemainderOrNull(skipFlags: false));
                if (requestedTag.IsEmpty) throw Errors.EmptyProxyTags(target);

                // This is mostly a legacy command, so it's gonna warn if there's
                // already more than one proxy tag.
                if (target.ProxyTags.Count > 1)
                {
                    var msg = $"This member already has more than one proxy tag set: {target.ProxyTagsString()}\nDo you want to replace them?";
                    if (!await ctx.PromptYesNo(msg))
                        throw Errors.GenericCancelled();
                }
                
                if (!await WarnOnConflict(requestedTag))
                    throw Errors.GenericCancelled();

                var newTags = new[] {requestedTag};
                var patch = new MemberPatch {ProxyTags = Partial<ProxyTag[]>.Present(newTags)};
                await _db.Execute(conn => _repo.UpdateMember(conn, target.Id, patch));
                
                await ctx.Reply($"{Emojis.Success} Member proxy tags set to {requestedTag.ProxyString.AsCode()}.");
            }
        }
    }
}