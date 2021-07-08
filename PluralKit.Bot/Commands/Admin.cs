using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using PluralKit.Bot.Interactive;
using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Admin
    {
        private readonly BotConfig _botConfig;
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;

        public Admin(BotConfig botConfig, IDatabase db, ModelRepository repo)
        {
            _botConfig = botConfig;
            _db = db;
            _repo = repo;
        }

        public async Task UpdateSystemId(Context ctx)
        {
            AssertBotAdmin(ctx);
            
            var target = await ctx.MatchSystem();
            if (target == null)
                throw new PKError("Unknown system.");

            var newHid = ctx.PopArgument();
            if (!Regex.IsMatch(newHid, "^[a-z]{5}$"))
                throw new PKError($"Invalid new system ID `{newHid}`.");

            var existingSystem = await _db.Execute(c => _repo.GetSystemByHid(c, newHid));
            if (existingSystem != null)
                throw new PKError($"Another system already exists with ID `{newHid}`.");

            var prompt = new YesNoPrompt(ctx)
            {
                Message = $"Change system ID of `{target.Hid}` to `{newHid}`?",
                AcceptLabel = "Change"
            };
            await prompt.Run();

            await _db.Execute(c => _repo.UpdateSystem(c, target.Id, new SystemPatch {Hid = newHid}));
            await ctx.Reply($"{Emojis.Success} System ID updated (`{target.Hid}` -> `{newHid}`).");
        }
        
        public async Task UpdateMemberId(Context ctx)
        {
            AssertBotAdmin(ctx);
            
            var target = await ctx.MatchMember();
            if (target == null)
                throw new PKError("Unknown member.");

            var newHid = ctx.PopArgument();
            if (!Regex.IsMatch(newHid, "^[a-z]{5}$"))
                throw new PKError($"Invalid new member ID `{newHid}`.");

            var existingMember = await _db.Execute(c => _repo.GetMemberByHid(c, newHid));
            if (existingMember != null)
                throw new PKError($"Another member already exists with ID `{newHid}`.");

            var prompt = new YesNoPrompt(ctx)
            {
                Message = $"Change member ID of **{target.NameFor(LookupContext.ByNonOwner)}** (`{target.Hid}`) to `{newHid}`?",
                AcceptLabel = "Change"
            };
            await prompt.Run();

            if (prompt.Result != true)
                throw new PKError("ID change cancelled.");
            
            await _db.Execute(c => _repo.UpdateMember(c, target.Id, new MemberPatch {Hid = newHid}));
            await ctx.Reply($"{Emojis.Success} Member ID updated (`{target.Hid}` -> `{newHid}`).");
        }

        public async Task UpdateGroupId(Context ctx)
        {
            AssertBotAdmin(ctx);

            var target = await ctx.MatchGroup();
            if (target == null)
                throw new PKError("Unknown group.");

            var newHid = ctx.PopArgument();
            if (!Regex.IsMatch(newHid, "^[a-z]{5}$"))
                throw new PKError($"Invalid new group ID `{newHid}`.");

            var existingGroup = await _db.Execute(c => _repo.GetGroupByHid(c, newHid));
            if (existingGroup != null)
                throw new PKError($"Another group already exists with ID `{newHid}`.");

            var prompt = new YesNoPrompt(ctx)
            {
                Message = $"Change group ID of **{target.Name}** (`{target.Hid}`) to `{newHid}`?",
                AcceptLabel = "Change"
            };
            await prompt.Run();

            if (prompt.Result != true)
                throw new PKError("ID change cancelled.");

            await _db.Execute(c => _repo.UpdateGroup(c, target.Id, new GroupPatch {Hid = newHid}));
            await ctx.Reply($"{Emojis.Success} Group ID updated (`{target.Hid}` -> `{newHid}`).");
        }

        public async Task SystemMemberLimit(Context ctx)
        {
            AssertBotAdmin(ctx);

            var target = await ctx.MatchSystem();
            if (target == null)
                throw new PKError("Unknown system.");

            var currentLimit = target.MemberLimitOverride ?? Limits.MaxMemberCount;
            if (!ctx.HasNext())
            {
                await ctx.Reply($"Current member limit is **{currentLimit}** members.");
                return;
            }

            var newLimitStr = ctx.PopArgument();
            if (!int.TryParse(newLimitStr, out var newLimit))
                throw new PKError($"Couldn't parse `{newLimitStr}` as number.");
                
            var prompt = new YesNoPrompt(ctx)
            {
                Message = $"Update member limit from **{currentLimit}** to **{newLimit}**?",
                AcceptLabel = "Update"
            };
            await prompt.Run();

            if (prompt.Result != true)
                throw new PKError("Member limit change cancelled.");
            
            await using var conn = await _db.Obtain();
            await _repo.UpdateSystem(conn, target.Id, new SystemPatch
            {
                MemberLimitOverride = newLimit
            });
            await ctx.Reply($"{Emojis.Success} Member limit updated.");
        }

        public async Task SystemGroupLimit(Context ctx)
        {
            AssertBotAdmin(ctx);

            var target = await ctx.MatchSystem();
            if (target == null)
                throw new PKError("Unknown system.");

            var currentLimit = target.GroupLimitOverride ?? Limits.MaxGroupCount;
            if (!ctx.HasNext())
            {
                await ctx.Reply($"Current group limit is **{currentLimit}** groups.");
                return;
            }

            var newLimitStr = ctx.PopArgument();
            if (!int.TryParse(newLimitStr, out var newLimit))
                throw new PKError($"Couldn't parse `{newLimitStr}` as number.");

            var prompt = new YesNoPrompt(ctx)
            {
                Message = $"Update group limit from **{currentLimit}** to **{newLimit}**?",
                AcceptLabel = "Update"
            };
            await prompt.Run();

            if (prompt.Result != true)
                throw new PKError("Group limit change cancelled.");

            await using var conn = await _db.Obtain();
            await _repo.UpdateSystem(conn, target.Id, new SystemPatch
            {
                GroupLimitOverride = newLimit
            });
            await ctx.Reply($"{Emojis.Success} Group limit updated.");
        }

        private void AssertBotAdmin(Context ctx)
        {
            if (!IsBotAdmin(ctx))
                throw new PKError("This command is only usable by bot admins.");
        }

        private bool IsBotAdmin(Context ctx)
        {
            return _botConfig.AdminRole != null && ctx.Member.Roles.Contains(_botConfig.AdminRole.Value);
        }
    }
}