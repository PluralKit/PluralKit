using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using PluralKit.API.Models;
using PluralKit.Core;

namespace PluralKit.API.v2
{
    public class PKControllerBase: ControllerBase
    {
        private readonly Guid _requestId = Guid.NewGuid();
        private readonly Regex _shortIdRegex = new Regex("^[a-z]{5}$");
        private readonly Regex _snowflakeRegex = new Regex("^[0-9]{17,19}$");

        protected readonly IDatabase Database;
        protected readonly ModelRepository Repo;
        protected readonly IAuthorizationService Authz;

        public PKControllerBase(IServiceProvider svc)
        {
            Database = svc.GetRequiredService<IDatabase>();
            Repo = svc.GetRequiredService<ModelRepository>();
            Authz = svc.GetRequiredService<IAuthorizationService>();
        }

        protected ApiError Error(ApiErrorCode errorCode, string message)
        {
            return new ApiError
            {
                Code = errorCode,
                Message = message,
                Timestamp = SystemClock.Instance.GetCurrentInstant(),
                RequestId = _requestId
            };
        }

        protected Task Authorize(PKSystem subject, string policy, FormattableString msg) =>
            Authorize((object) subject, policy, msg);

        protected Task Authorize(PKMember subject, string policy, FormattableString msg) =>
            Authorize((object) subject, policy, msg);

        private async Task Authorize(object subject, string policy, FormattableString msg)
        {
            var res = await Authz.AuthorizeAsync(User, subject, policy);
            if (res.Succeeded)
                return;
            
            throw new ApiErrorException(HttpStatusCode.Forbidden, Error(ApiErrorCode.NoPermission, msg.ToString()));
        }

        protected async Task<PKSystem> ResolveSystem(string systemRef)
        {
            await using var conn = await Database.Obtain();

            if (systemRef.Equals("me", StringComparison.InvariantCultureIgnoreCase))
                return await ResolveOwnSystem(conn);

            if (Guid.TryParse(systemRef, out var guid))
                return await ResolveSystemByGuid(conn, guid);

            if (_snowflakeRegex.IsMatch(systemRef))
                return await ResolveSystemByAccount(conn, ulong.Parse(systemRef));

            if (_shortIdRegex.IsMatch(systemRef))
                return await ResolveSystemByShortId(conn, systemRef);

            throw new ApiErrorException(HttpStatusCode.BadRequest,
                Error(ApiErrorCode.InvalidSystemReference,
                    $"Invalid system reference '{systemRef}' (must be UUID, short ID, Discord user snowflake, or 'me')"));
        }

        protected async Task<PKMember> ResolveMember(string memberRef)
        {
            await using var conn = await Database.Obtain();

            if (Guid.TryParse(memberRef, out var guid))
                return await ResolveMemberByGuid(conn, guid);

            if (_shortIdRegex.IsMatch(memberRef))
                return await ResolveMemberByShortId(conn, memberRef);

            throw new ApiErrorException(HttpStatusCode.BadRequest,
                Error(ApiErrorCode.InvalidMemberReference,
                    $"Invalid member reference '{memberRef}' (must be UUID or short ID)"));
        }
        
        protected async Task<PKGroup> ResolveGroup(string groupRef)
        {
            await using var conn = await Database.Obtain();

            if (Guid.TryParse(groupRef, out var guid))
                return await ResolveGroupByGuid(conn, guid);

            if (_shortIdRegex.IsMatch(groupRef))
                return await ResolveGroupByShortId(conn, groupRef);

            throw new ApiErrorException(HttpStatusCode.BadRequest,
                Error(ApiErrorCode.InvalidGroupReference,
                    $"Invalid group reference '{groupRef}' (must be UUID or short ID)"));
        }

        private async Task<PKSystem> ResolveOwnSystem(IPKConnection conn)
        {
            if (!User.TryGetCurrentSystem(out var systemId))
            {
                throw new ApiErrorException(HttpStatusCode.Unauthorized,
                    Error(ApiErrorCode.NotAuthenticated,
                        "Must be authenticated with a system token to look up the current system"));
            }

            return await Repo.GetSystem(conn, systemId);
        }

        private async Task<PKSystem> ResolveSystemByGuid(IPKConnection conn, Guid guid)
        {
            var sys = await Repo.GetSystemByGuid(conn, guid);
            if (sys == null)
                throw new ApiErrorException(HttpStatusCode.NotFound,
                    Error(ApiErrorCode.SystemNotFound, $"System with ID {guid} not found"));
            return sys;
        }

        private async Task<PKSystem> ResolveSystemByShortId(IPKConnection conn, string shortId)
        {
            var sys = await Repo.GetSystemByHid(conn, shortId);
            if (sys == null)
                throw new ApiErrorException(HttpStatusCode.NotFound,
                    Error(ApiErrorCode.SystemNotFound, $"System with short ID '{shortId}' not found"));
            return sys;
        }

        private async Task<PKSystem> ResolveSystemByAccount(IPKConnection conn, ulong account)
        {
            var sys = await Repo.GetSystemByAccount(conn, account);
            if (sys == null)
                throw new ApiErrorException(HttpStatusCode.NotFound,
                    Error(ApiErrorCode.SystemNotFound, $"System linked to account '{account}' not found"));
            return sys;
        }

        private async Task<PKMember> ResolveMemberByGuid(IPKConnection conn, Guid guid)
        {
            var member = await Repo.GetMemberByGuid(conn, guid);
            if (member == null)
                throw new ApiErrorException(HttpStatusCode.NotFound,
                    Error(ApiErrorCode.MemberNotFound, $"Member with ID {guid} not found"));
            return member;
        }

        private async Task<PKMember> ResolveMemberByShortId(IPKConnection conn, string shortId)
        {
            var member = await Repo.GetMemberByHid(conn, shortId);
            if (member == null)
                throw new ApiErrorException(HttpStatusCode.NotFound,
                    Error(ApiErrorCode.MemberNotFound, $"Member with short ID '{shortId} not found"));
            return member;
        }

        private async Task<PKGroup> ResolveGroupByGuid(IPKConnection conn, Guid guid)
        {
            var group = await Repo.GetGroupByGuid(conn, guid);
            if (group == null)
                throw new ApiErrorException(HttpStatusCode.NotFound,
                    Error(ApiErrorCode.GroupNotFound, $"Group with ID {guid} not found"));
            return group;
        }

        private async Task<PKGroup> ResolveGroupByShortId(IPKConnection conn, string shortId)
        {
            var group = await Repo.GetGroupByHid(conn, shortId);
            if (group == null)
                throw new ApiErrorException(HttpStatusCode.NotFound,
                    Error(ApiErrorCode.GroupNotFound, $"Group with short ID '{shortId}' not found"));
            return group;
        }
    }
}