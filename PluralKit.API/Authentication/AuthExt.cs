using System;
using System.Security.Claims;

using PluralKit.Core;

namespace PluralKit.API
{
    public static class AuthExt
    {
        public static bool TryGetCurrentSystem(this ClaimsPrincipal user, out SystemId currentSystem)
        {
            currentSystem = default;
            
            var claim = user.FindFirst(PKClaims.SystemId);
            if (claim == null)
                return false;

            if (!int.TryParse(claim.Value, out var id))
                throw new ArgumentException("User has non-integer system ID claim");
            
            currentSystem = new SystemId(id);
            return true;
        }
        
        public static SystemId CurrentSystem(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst(PKClaims.SystemId);
            if (claim == null) throw new ArgumentException("User is unauthorized");
            
            if (int.TryParse(claim.Value, out var id))
                return new SystemId(id);
            throw new ArgumentException("User has non-integer system ID claim");
        }
        
        public static LookupContext ContextFor(this ClaimsPrincipal user, PKSystem system)
        {
            if (!user.Identity.IsAuthenticated) return LookupContext.API;
            return system.Id == user.CurrentSystem() ? LookupContext.ByOwner : LookupContext.API;
        }

        public static LookupContext ContextFor(this ClaimsPrincipal user, PKMember member)
        {
            if (!user.Identity.IsAuthenticated) return LookupContext.API;
            return member.System == user.CurrentSystem() ? LookupContext.ByOwner : LookupContext.API;
        }
    }
}