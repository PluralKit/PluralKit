using System.Linq;
using System.Threading.Tasks;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class MemberProxy
    {
        private IDataStore _data;
        
        public MemberProxy(IDataStore data)
        {
            _data = data;
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
                var conflicts = (await _data.GetConflictingProxies(ctx.System, newTag))
                    .Where(m => m.Id != target.Id)
                    .ToList();

                if (conflicts.Count <= 0) return true;

                var conflictList = conflicts.Select(m => $"- **{m.Name}**");
                var msg = await ctx.Reply(
                    $"{Emojis.Warn} The following members have conflicting proxy tags:\n{string.Join('\n', conflictList)}\nDo you want to proceed anyway?");
                return await ctx.PromptYesNo(msg);
            }
            
            // "Sub"command: no arguments clearing
            // Also matches the pseudo-subcommand "text" which is equivalent to emoty proxy tags on both sides.
            if (!ctx.HasNext() || ctx.Match("text"))
            {
                // If we already have multiple tags, this would clear everything, so prompt that
                if (target.ProxyTags.Count > 1)
                {
                    var msg = await ctx.Reply(
                        $"{Emojis.Warn} You already have multiple proxy tags set: {target.ProxyTagsString()}\nDo you want to clear them all?");
                    if (!await ctx.PromptYesNo(msg))
                        throw Errors.GenericCancelled();
                }
                
                target.ProxyTags = new ProxyTag[] { };
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Proxy tags cleared.");
            }
            // Subcommand: "add"
            else if (ctx.Match("add"))
            {
                if (!ctx.HasNext()) throw new PKSyntaxError("You must pass an example proxy to add (eg. `[text]` or `J:text`).");
                
                var tagToAdd = ParseProxyTags(ctx.RemainderOrNull());
                if (target.ProxyTags.Contains(tagToAdd))
                    throw Errors.ProxyTagAlreadyExists(tagToAdd, target);
                
                if (!await WarnOnConflict(tagToAdd))
                    throw Errors.GenericCancelled();

                // It's not guaranteed the list's mutable, so we force it to be
                target.ProxyTags = target.ProxyTags.ToList();
                target.ProxyTags.Add(tagToAdd);
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Added proxy tags `{tagToAdd.ProxyString.SanitizeMentions()}`.");
            }
            // Subcommand: "remove"
            else if (ctx.Match("remove"))
            {
                if (!ctx.HasNext()) throw new PKSyntaxError("You must pass a proxy tag to remove (eg. `[text]` or `J:text`).");

                var tagToRemove = ParseProxyTags(ctx.RemainderOrNull());
                if (!target.ProxyTags.Contains(tagToRemove))
                    throw Errors.ProxyTagDoesNotExist(tagToRemove, target);

                // It's not guaranteed the list's mutable, so we force it to be
                target.ProxyTags = target.ProxyTags.ToList();
                target.ProxyTags.Remove(tagToRemove);
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Removed proxy tags `{tagToRemove.ProxyString.SanitizeMentions()}`.");
            }
            // Subcommand: bare proxy tag given
            else
            {
                if (!ctx.HasNext()) throw new PKSyntaxError("You must pass an example proxy to set (eg. `[text]` or `J:text`).");

                var requestedTag = ParseProxyTags(ctx.RemainderOrNull());
                
                // This is mostly a legacy command, so it's gonna error out if there's
                // already more than one proxy tag.
                if (target.ProxyTags.Count > 1)
                    throw Errors.LegacyAlreadyHasProxyTag(requestedTag, target);
                
                if (!await WarnOnConflict(requestedTag))
                    throw Errors.GenericCancelled();

                target.ProxyTags = new[] {requestedTag};
                
                await _data.SaveMember(target);
                await ctx.Reply($"{Emojis.Success} Member proxy tags set to `{requestedTag.ProxyString.SanitizeMentions()}`.");
            }
        }
    }
}