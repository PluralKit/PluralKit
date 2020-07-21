using PluralKit.Core;


namespace PluralKit.Bot
{
    public static class ContextCardExt
    {
        public static CardOptions ParseCardOptions(this Context ctx, LookupContext lookupCtx)
        {
            var p = new CardOptions();

            // Privacy filter (default is public fields only)
            if (ctx.MatchFlag("a", "all")) p.PrivacyFilter = null; 
            if (ctx.MatchFlag("s","safe")) p.PrivacyFilter = PrivacyLevel.Public;

            // PERM CHECK: If we're trying to access private fields of another system, error
            if (p.PrivacyFilter != PrivacyLevel.Public && lookupCtx != LookupContext.ByOwner)
                throw new PKError("You cannot look up private fields of another system's member.");
            
            // Done!
            return p;
        }
    }
}