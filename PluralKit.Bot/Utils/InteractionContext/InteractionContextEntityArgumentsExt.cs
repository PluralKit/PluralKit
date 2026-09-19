using PluralKit.Core;

namespace PluralKit.Bot;

public static class InteractionContextEntityArgumentsExt
{
    public static async Task<PKSystem> MatchSystem(this InteractionContext ctx)
    {
        // System references can take three forms:
        // - The direct user ID of an account connected to the system
        // - A @mention of an account connected to the system (<@uid>)
        // - A system hid
        var idRef = ctx.OptionValue("id")?.ToString();
        var user = ctx.OptionValue("account")?.ToString();

        if (idRef != null && user != null)
            throw new PKError("Please only use reference by ID *or* by Account");

        string input = idRef ?? user ?? ctx.Author.Id.ToString();

        PKSystem? system = null;

        // Direct IDs and mentions are both handled by the below method:
        if (input.TryParseMention(out var id))
            system = await ctx.Repository.GetSystemByAccount(id);

        // Finally, try HID parsing
        if (input.TryParseHid(out var hid))
            system = await ctx.Repository.GetSystemByHid(hid);

        // If there is no system, throw an error
        ctx.CheckSystem(system);
        return system;
    }
}