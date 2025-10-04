using System.Text.RegularExpressions;

using Myriad.Extensions;
using Myriad.Types;

using PluralKit.Bot.Utils;
using PluralKit.Core;

namespace PluralKit.Bot;

public static class ContextEntityArgumentsExt
{
    public static async Task<User> ParseUser(this Context ctx, string arg)
    {
        if (arg.TryParseMention(out var id))
            return await ctx.Cache.GetOrFetchUser(ctx.Rest, id);

        return null;
    }

    public static async Task<PKSystem> ParseSystem(this Context ctx, string input)
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

    public static async Task<PKMember?> ParseMember(this Context ctx, string input, bool byId)
    {
        // Member references can have one of three forms, depending on
        // whether you're in a system or not:
        // - A member hid
        // - A textual name of a member *in your own system*
        // - a textual display name of a member *in your own system*

        // Skip name / display name matching if the user does not have a system
        // or if they specifically request by-HID matching
        if (ctx.System != null && !byId)
        {
            // First, try finding by member name in system
            if (await ctx.Repository.GetMemberByName(ctx.System.Id, input) is PKMember memberByName)
                return memberByName;

            // And if that fails, we try finding a member with a display name matching the argument from the system
            if (ctx.System != null &&
                await ctx.Repository.GetMemberByDisplayName(ctx.System.Id, input) is PKMember memberByDisplayName)
                return memberByDisplayName;
        }

        // Finally (or if by-HID lookup is specified), check if input is a valid HID and then try member HID parsing:

        if (!input.TryParseHid(out var hid))
            return null;

        // For posterity:
        // There was a bug that made `SELECT * FROM MEMBERS WHERE HID = $1` hang forever BUT 
        // `SELECT * FROM MEMBERS WHERE HID = $1 AND SYSTEM = $2` *doesn't* hang! So this is a bandaid for that

        // If we are supposed to restrict it to a system anyway we can just do that
        PKMember memberByHid = null;
        memberByHid = await ctx.Repository.GetMemberByHid(hid, ctx.System?.Id);
        if (memberByHid != null)
            return memberByHid;

        // ff ctx.System was null then this would be a duplicate of above and we don't want to run it again
        if (ctx.System != null)
        {
            memberByHid = await ctx.Repository.GetMemberByHid(hid);
            if (memberByHid != null)
                return memberByHid;
        }

        // We didn't find anything, so we return null.
        return null;
    }

    public static async Task<PKGroup> ParseGroup(this Context ctx, string input, bool byId, SystemId? restrictToSystem = null)
    {
        if (ctx.System != null && !byId)
        {
            if (await ctx.Repository.GetGroupByName(ctx.System.Id, input) is { } byName)
                return byName;
            if (await ctx.Repository.GetGroupByDisplayName(ctx.System.Id, input) is { } byDisplayName)
                return byDisplayName;
        }

        if (!input.TryParseHid(out var hid))
            return null;

        if (await ctx.Repository.GetGroupByHid(hid, restrictToSystem) is { } byHid)
            return byHid;

        return null;
    }

    public static string CreateNotFoundError(this Context ctx, string entity, string input, bool byId = false)
    {
        var isIDOnlyQuery = ctx.System == null || byId;
        var inputIsHid = HidUtils.ParseHid(input) != null;

        if (isIDOnlyQuery)
        {
            if (inputIsHid)
                return $"{entity} with ID \"{input}\" not found.";
            return $"{entity} not found. Note that a {entity.ToLower()} ID is 5 or 6 characters long.";
        }

        if (inputIsHid)
            return $"{entity} with ID or name \"{input}\" not found.";
        return $"{entity} with name \"{input}\" not found. Note that a {entity.ToLower()} ID is 5 or 6 characters long.";
    }

    public static async Task<Channel> MatchChannel(this Context ctx)
    {
        if (!MentionUtils.TryParseChannel(ctx.PeekArgument(), out var id))
            return null;

        // todo: match channels in other guilds
        var channel = await ctx.Cache.TryGetChannel(ctx.Guild!.Id, id);
        if (channel == null)
            channel = await ctx.Rest.GetChannelOrNull(id);
        if (channel == null)
            return null;

        if (!DiscordUtils.IsValidGuildChannel(channel))
            return null;

        ctx.PopArgument();
        return channel;
    }
}