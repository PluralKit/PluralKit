using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DSharpPlus.Entities;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class MemberGroup
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;

        public MemberGroup(IDatabase db, ModelRepository repo)
        {
            _db = db;
            _repo = repo;
        }

        private String groupTerm(int groups) => groups == 1 ? "group" : "groups";
        
        private String Response(List<GroupId> groupList, List<GroupId> actionedOn, Groups.AddRemoveOperation op)
        {
            var opStr = op == Groups.AddRemoveOperation.Add ? "added to" : "removed from";
            var inStr = op == Groups.AddRemoveOperation.Add ? "in" : "not in";
            var notActionedOn = groupList.Count - actionedOn.Count;

            if (notActionedOn == 0)
                return $"{Emojis.Success} Member {opStr} {groupTerm(actionedOn.Count)}.";
            else
                return $"{Emojis.Success} Member {opStr} {actionedOn.Count} {groupTerm(actionedOn.Count)} (member already {inStr} {notActionedOn} {groupTerm(notActionedOn)}).";
        }

        public async Task AddRemove(Context ctx, PKMember target, Groups.AddRemoveOperation op)
        {
            ctx.CheckSystem().CheckOwnMember(target);

            var groups = (await ctx.ParseGroupList(ctx.System.Id))
                .Select(g => g.Id)
                .ToList();

            await using var conn = await _db.Obtain();
            var existingGroups = (await _repo.GetMemberGroups(conn, target.Id).ToListAsync())
                .Select(g => g.Id)
                .ToList();

            List<GroupId> toAction;

            if (op == Groups.AddRemoveOperation.Add)
            {
                toAction = groups
                    .Where(group => !existingGroups.Contains(group))
                    .ToList();

                await _repo.AddGroupsToMember(conn, target.Id, toAction);
            }
            else if (op == Groups.AddRemoveOperation.Remove)
            {
                toAction = groups
                    .Where(group => existingGroups.Contains(group))
                    .ToList();

                await _repo.RemoveGroupsFromMember(conn, target.Id, toAction);
            }
            else return; // otherwise toAction "may be unassigned"

            await ctx.Reply(Response(groups, toAction, op));
        }

        public async Task List(Context ctx, PKMember target)
        {
            await using var conn = await _db.Obtain();

            var pctx = ctx.LookupContextFor(target.System);

            var groups = await _repo.GetMemberGroups(conn, target.Id)
                .Where(g => g.Visibility.CanAccess(pctx))
                .OrderBy(g => g.Name, StringComparer.InvariantCultureIgnoreCase)
                .ToListAsync();

            var description = "";
            var msg = "";

            if (groups.Count == 0)
                description = "This member has no groups.";
            else
                description = string.Join("\n", groups.Select(g => $"[`{g.Hid}`] **{g.DisplayName ?? g.Name}**"));
            
            if (pctx == LookupContext.ByOwner)
            {
                msg += $"\n\nTo add this member to one or more groups, use `pk;m {target.Reference()} group add <group> [group 2] [group 3...]`";
                if (groups.Count > 0)
                    msg += $"\nTo remove this member from one or more groups, use `pk;m {target.Reference()} group remove <group> [group 2] [group 3...]`";
            }
            
            await ctx.Reply(msg, embed: (new DiscordEmbedBuilder().WithTitle($"{target.Name}'s groups").WithDescription(description)).Build());
        }
    }
}