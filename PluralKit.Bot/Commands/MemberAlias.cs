using PluralKit.Core;

namespace PluralKit.Bot;

public class MemberAlias
{
    public async Task Alias(Context ctx, PKMember target)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        // "Sub"command: clear flag
        if (ctx.MatchClear())
        {
            // If we already have multiple aliases, this would clear everything, so prompt that
            if (target.Aliases.Count > 1)
            {
                var msg = $"{Emojis.Warn} You already have multiple aliases set: {string.Join(", ", target.Aliases)}\nDo you want to clear them all?";
                if (!await ctx.PromptYesNo(msg, "Clear"))
                    throw Errors.GenericCancelled();
            }

            var patch = new MemberPatch { Aliases = Partial<string[]>.Present(new string[0]) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Aliases cleared.");
        }
        // "Sub"command: no arguments; will print aliases
        else if (!ctx.HasNext(false))
        {
            if (target.Aliases.Count == 0)
                await ctx.Reply("This member does not have any aliases.");
            else
                await ctx.Reply($"This member's aliases are:\n{string.Join('\n', target.Aliases.Select(a => $"- {a}"))}");
        }
        // Subcommand: "add"
        else if (ctx.Match("add", "append"))
        {
            if (!ctx.HasNext(false))
                throw new PKSyntaxError("You must pass an alias to add.");

            var aliasToAdd = ctx.RemainderOrNull(false).NormalizeLineEndSpacing().Trim();
            if (aliasToAdd.Length == 0)
                throw new PKSyntaxError("You must pass an alias to add.");
            if (aliasToAdd.Length > Limits.MaxMemberNameLength)
                throw Errors.StringTooLongError("Alias", aliasToAdd.Length, Limits.MaxMemberNameLength);
            if (target.Aliases.Any(a => a.Equals(aliasToAdd, StringComparison.OrdinalIgnoreCase)))
                throw new PKError("This member already has that alias.");

            // Warn (but don't block) if this alias is already set as a name, display name, or alias elsewhere
            var conflicts = await ctx.FindConflictingMembers(target.Id, aliasToAdd);
            if (conflicts.Count > 0)
            {
                var conflictList = conflicts.Select(m => $"- **{m.NameFor(ctx)}** (`{m.DisplayHid(ctx.Config)}`)");
                var msg = $"{Emojis.Warn} The following members already have this set as a name, display name, or alias:\n{string.Join('\n', conflictList)}\nDo you want to add this alias anyway?";
                if (!await ctx.PromptYesNo(msg, "Add"))
                    throw Errors.GenericCancelled();
            }

            var newAliases = target.Aliases.ToList();
            newAliases.Add(aliasToAdd);
            var patch = new MemberPatch { Aliases = Partial<string[]>.Present(newAliases.ToArray()) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Added alias {aliasToAdd.AsCode()} (using {aliasToAdd.Length}/{Limits.MaxMemberNameLength} characters).");
        }
        // Subcommand: "remove"
        else if (ctx.Match("remove", "delete"))
        {
            if (!ctx.HasNext(false))
                throw new PKSyntaxError("You must pass an alias to remove.");

            var aliasToRemove = ctx.RemainderOrNull(false).NormalizeLineEndSpacing().Trim();
            var existing = target.Aliases.FirstOrDefault(a => a.Equals(aliasToRemove, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
                throw new PKError($"This member does not have the alias {aliasToRemove.AsCode()}.");

            var newAliases = target.Aliases.ToList();
            newAliases.Remove(existing);
            var patch = new MemberPatch { Aliases = Partial<string[]>.Present(newAliases.ToArray()) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Removed alias {existing.AsCode()}.");
        }
        else
        {
            throw new PKSyntaxError("You must pass a subcommand: `add`, `remove`, or `clear`.");
        }
    }
}
