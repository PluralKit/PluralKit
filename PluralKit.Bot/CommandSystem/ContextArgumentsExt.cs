using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public static class ContextArgumentsExt
    {
        public static string PopArgument(this Context ctx) =>
            ctx.Parameters.Pop();
        
        public static string PeekArgument(this Context ctx) => 
            ctx.Parameters.Peek();

        public static string RemainderOrNull(this Context ctx, bool skipFlags = true) =>
            ctx.Parameters.Remainder(skipFlags).Length == 0 ? null : ctx.Parameters.Remainder(skipFlags);

        public static bool HasNext(this Context ctx, bool skipFlags = true) =>
            ctx.RemainderOrNull(skipFlags) != null;
        
        public static string FullCommand(this Context ctx) => 
            ctx.Parameters.FullCommand;
        
        /// <summary>
        /// Checks if the next parameter is equal to one of the given keywords. Case-insensitive.
        /// </summary>
        public static bool Match(this Context ctx, ref string used, params string[] potentialMatches)
        {
            var arg = ctx.PeekArgument();
            foreach (var match in potentialMatches)
            {
                if (arg.Equals(match, StringComparison.InvariantCultureIgnoreCase))
                {
                    used = ctx.PopArgument();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the next parameter is equal to one of the given keywords. Case-insensitive.
        /// </summary>
        public static bool Match(this Context ctx, params string[] potentialMatches)
        {
            string used = null; // Unused and unreturned, we just yeet it
            return ctx.Match(ref used, potentialMatches);
        }

        public static bool MatchFlag(this Context ctx, params string[] potentialMatches)
        {
            // Flags are *ALWAYS PARSED LOWERCASE*. This means we skip out on a "ToLower" call here.
            // Can assume the caller array only contains lowercase *and* the set below only contains lowercase
            
            var flags = ctx.Parameters.Flags();
            return potentialMatches.Any(potentialMatch => flags.Contains(potentialMatch));
        }

        public static async Task<bool> MatchClear(this Context ctx, string toClear = null)
        {
            var matched = ctx.Match("clear", "reset") || ctx.MatchFlag("c", "clear");
            if (matched && toClear != null) 
                return await ctx.ConfirmClear(toClear);
            return matched;
        }

        public static async Task<List<PKMember>> ParseMemberList(this Context ctx, SystemId? restrictToSystem)
        {
            var members = new List<PKMember>();

            // Loop through all the given arguments
            while (ctx.HasNext())
            {
                // and attempt to match a member 
                var member = await ctx.MatchMember();
                if (member == null)
                    // if we can't, big error. Every member name must be valid.
                    throw new PKError(ctx.CreateMemberNotFoundError(ctx.PopArgument()));

                if (restrictToSystem != null && member.System != restrictToSystem)
                    throw Errors.NotOwnMemberError; // TODO: name *which* member?
                
                members.Add(member); // Then add to the final output list
            }
            if (members.Count == 0) throw new PKSyntaxError($"You must input at least one member.");
            
            return members;
        }

        public static async Task<List<PKGroup>> ParseGroupList(this Context ctx, SystemId? restrictToSystem)
        {
            var groups = new List<PKGroup>();

            // Loop through all the given arguments
            while (ctx.HasNext())
            {
                // and attempt to match a group 
                var group = await ctx.MatchGroup();
                if (group == null)
                    // if we can't, big error. Every group name must be valid.
                    throw new PKError(ctx.CreateGroupNotFoundError(ctx.PopArgument()));

                if (restrictToSystem != null && group.System != restrictToSystem)
                    throw Errors.NotOwnGroupError; // TODO: name *which* group?
                
                groups.Add(group); // Then add to the final output list
            }
            
            if (groups.Count == 0) throw new PKSyntaxError($"You must input at least one group.");

            return groups;
        }

        public static AutoproxyScope MatchAutoproxyScope(this Context ctx)
        {
            // TODO: how do we match guild/channel IDs here?

            if (ctx.MatchFlag("global"))
                return AutoproxyScope.Global;
            else if (ctx.MatchFlag("server", "guild"))
                return AutoproxyScope.Guild;
            else if (ctx.MatchFlag("channel"))
                return AutoproxyScope.Channel;
            
            // default fallback
            return AutoproxyScope.Guild;
        }
    }
}
