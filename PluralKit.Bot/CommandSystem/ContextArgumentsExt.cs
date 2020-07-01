using System;
using System.Linq;

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
    }
}