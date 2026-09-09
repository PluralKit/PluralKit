using PluralKit.Core;

namespace PluralKit.Bot;

public static class InteractionContextEntityArgumentsExt
{
    public static async Task<PKSystem> MatchSystem(this InteractionContext ctx, string input)
    {

        // System references can take three forms:
        // - The direct user ID of an account connected to the system
        // - A @mention of an account connected to the system (<@uid>)
        // - A system hid

        // Direct IDs and mentions are both handled by the below method:
        if (input.TryParseMention(out var id))
            return await ctx.Repository.GetSystemByAccount(id);

        // Finally, try HID parsing
        if (input.TryParseHid(out var hid))
            return await ctx.Repository.GetSystemByHid(hid);

        return null;
    }
}

