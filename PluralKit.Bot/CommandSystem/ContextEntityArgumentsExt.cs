using System.Threading.Tasks;

using Myriad.Extensions;
using Myriad.Types;

using PluralKit.Bot.Utils;
using PluralKit.Core;

namespace PluralKit.Bot
{
    public static class ContextEntityArgumentsExt
    {
        public static async Task<User> MatchUser(this Context ctx)
        {
            var text = ctx.PeekArgument();
            if (text.TryParseMention(out var id))
                return await ctx.Cache.GetOrFetchUser(ctx.Rest, id);

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

            await using var conn = await ctx.Database.Obtain();
            
            // Direct IDs and mentions are both handled by the below method:
            if (input.TryParseMention(out var id))
                return await ctx.Repository.GetSystemByAccount(conn, id);

            // Finally, try HID parsing
            var system = await ctx.Repository.GetSystemByHid(conn, input);
            return system;
        }

        public static async Task<PKMember> PeekMember(this Context ctx)
        {
            var input = ctx.PeekArgument();

            // Member references can have one of three forms, depending on
            // whether you're in a system or not:
            // - A member hid
            // - A textual name of a member *in your own system*
            // - a textual display name of a member *in your own system*

            // First, if we have a system, try finding by member name in system
            await using var conn = await ctx.Database.Obtain();
            if (ctx.System != null && await ctx.Repository.GetMemberByName(conn, ctx.System.Id, input) is PKMember memberByName)
                return memberByName;

            // Then, try member HID parsing:
            if (await ctx.Repository.GetMemberByHid(conn, input) is PKMember memberByHid)
                return memberByHid;

            // And if that again fails, we try finding a member with a display name matching the argument from the system
            if (ctx.System != null && await ctx.Repository.GetMemberByDisplayName(conn, ctx.System.Id, input) is PKMember memberByDisplayName)
                return memberByDisplayName;
            
            // We didn't find anything, so we return null.
            return null;
        }

        /// <summary>
        /// Attempts to pop a member descriptor from the stack, returning it if present. If a member could not be
        /// resolved by the next word in the argument stack, does *not* touch the stack, and returns null.
        /// </summary>
        public static async Task<PKMember> MatchMember(this Context ctx)
        {
            // First, peek a member
            var member = await ctx.PeekMember();

            // If the peek was successful, we've used up the next argument, so we pop that just to get rid of it.
            if (member != null) ctx.PopArgument();

            // Finally, we return the member value.
            return member;
        }
        
        public static async Task<PKGroup> PeekGroup(this Context ctx)
        {
            var input = ctx.PeekArgument();

            await using var conn = await ctx.Database.Obtain();
            if (ctx.System != null && await ctx.Repository.GetGroupByName(conn, ctx.System.Id, input) is {} byName)
                return byName;
            if (await ctx.Repository.GetGroupByHid(conn, input) is {} byHid)
                return byHid;
            if (await ctx.Repository.GetGroupByDisplayName(conn, ctx.System.Id, input) is {} byDisplayName)
                return byDisplayName;

            return null;
        }

        public static async Task<PKGroup> MatchGroup(this Context ctx)
        {
            var group = await ctx.PeekGroup();
            if (group != null) ctx.PopArgument();
            return group;
        }

        public static string CreateMemberNotFoundError(this Context ctx, string input)
        {
            // TODO: does this belong here?
            if (input.Length == 5)
            {
                if (ctx.System != null)
                    return $"Member with ID or name \"{input}\" not found.";
                return $"Member with ID \"{input}\" not found."; // Accounts without systems can't query by name
            }

            if (ctx.System != null)
                return $"Member with name \"{input}\" not found. Note that a member ID is 5 characters long.";
            return $"Member not found. Note that a member ID is 5 characters long.";
        }
        
        public static string CreateGroupNotFoundError(this Context ctx, string input)
        {
            // TODO: does this belong here?
            if (input.Length == 5)
            {
                if (ctx.System != null)
                    return $"Group with ID or name \"{input}\" not found.";
                return $"Group with ID \"{input}\" not found."; // Accounts without systems can't query by name
            }

            if (ctx.System != null)
                return $"Group with name \"{input}\" not found. Note that a group ID is 5 characters long.";
            return $"Group not found. Note that a group ID is 5 characters long.";
        }
        
        public static async Task<Channel> MatchChannel(this Context ctx)
        {
            if (!MentionUtils.TryParseChannel(ctx.PeekArgument(), out var id)) 
                return null;

            if (!ctx.Cache.TryGetChannel(id, out var channel))
                return null;
            
            if (!(DiscordUtils.IsValidGuildChannel(channel))) 
                return null;
            
            ctx.PopArgument();
            return channel;
        }
    }
}