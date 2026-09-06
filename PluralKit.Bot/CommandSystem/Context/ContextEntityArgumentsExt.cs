using System.Text.RegularExpressions;

using Myriad.Extensions;
using Myriad.Types;

using PluralKit.Bot.Utils;
using PluralKit.Core;

namespace PluralKit.Bot;

public static class ContextEntityArgumentsExt
{
    public static async Task<User> MatchUser(this Context ctx)
    {
        var text = ctx.PeekArgument();
        if (text.TryParseMention(out var id))
        {
            var user = await ctx.Cache.GetOrFetchUser(ctx.Rest, id);
            if (user != null) ctx.PopArgument();
            return user;
        }

        return null;
    }

    public static bool MatchUserRaw(this Context ctx, out ulong id)
    {
        id = 0;

        var text = ctx.PeekArgument();
        if (text.TryParseMention(out var mentionId))
            id = mentionId;

        return id != 0;
    }

    public static Task<PKSystem> PeekSystem(this Context ctx) => ctx.MatchSystemInner();

    public static async Task<PKSystem> MatchSystem(this Context ctx)
    {
        var system = await ctx.MatchSystemInner();
        if (system != null) ctx.PopArgument();
        return system;
    }

    private static async Task<PKSystem> MatchSystemInner(this Context ctx)
    {
        var input = ctx.PeekArgument();

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

    public static async Task<PKMember> PeekMember(this Context ctx, SystemId? restrictToSystem = null)
    {
        var input = ctx.PeekArgument();

        // Member references can have one of three forms, depending on
        // whether you're in a system or not:
        // - A member hid
        // - A textual name of a member *in your own system*
        // - a textual display name of a member *in your own system*

        // Skip name / display name matching if the user does not have a system
        // or if they specifically request by-HID matching
        if (ctx.System != null && !ctx.MatchFlag("id", "by-id"))
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
        if (restrictToSystem != null)
        {
            memberByHid = await ctx.Repository.GetMemberByHid(hid, restrictToSystem);
            if (memberByHid != null)
                return memberByHid;
        }
        // otherwise we try the querier's system and if that doesn't work we do global
        else
        {
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
        }

        // We didn't find anything, so we return null.
        return null;
    }

    /// <summary>
    /// Attempts to pop a member descriptor from the stack, returning it if present. If a member could not be
    /// resolved by the next word in the argument stack, does *not* touch the stack, and returns null.
    /// </summary>
    public static async Task<PKMember> MatchMember(this Context ctx, SystemId? restrictToSystem = null)
    {
        // First, peek a member
        var member = await ctx.PeekMember(restrictToSystem);

        // If the peek was successful, we've used up the next argument, so we pop that just to get rid of it.
        if (member != null) ctx.PopArgument();

        // Finally, we return the member value.
        return member;
    }

    public static async Task<PKGroup> PeekGroup(this Context ctx, SystemId? restrictToSystem = null)
    {
        var input = ctx.PeekArgument();

        // see PeekMember for an explanation of the logic used here

        if (ctx.System != null && !ctx.MatchFlag("id", "by-id"))
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

    public static async Task<PKGroup> MatchGroup(this Context ctx, SystemId? restrictToSystem = null)
    {
        var group = await ctx.PeekGroup(restrictToSystem);
        if (group != null) ctx.PopArgument();
        return group;
    }

    public static string CreateNotFoundError(this Context ctx, string entity, string input)
    {
        var isIDOnlyQuery = ctx.System == null || ctx.MatchFlag("id", "by-id");
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

    public static async Task<Guild> MatchGuild(this Context ctx)
    {
        if (!ulong.TryParse(ctx.PeekArgument(), out var id))
            return null;

        var guild = await ctx.Rest.GetGuildOrNull(id);
        if (guild != null)
            ctx.PopArgument();

        return guild;
    }
}