using System.Text.RegularExpressions;

using Dapper;
using SqlKata;

using Myriad.Rest;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Admin
{
    private readonly BotConfig _botConfig;
    private readonly DiscordApiClient _rest;

    public Admin(BotConfig botConfig, DiscordApiClient rest)
    {
        _botConfig = botConfig;
        _rest = rest;
    }

    public async Task UpdateSystemId(Context ctx)
    {
        ctx.AssertBotAdmin();

        var target = await ctx.MatchSystem();
        if (target == null)
            throw new PKError("Unknown system.");

        var input = ctx.PopArgument();
        if (!input.TryParseHid(out var newHid))
            throw new PKError($"Invalid new system ID `{input}`.");

        var existingSystem = await ctx.Repository.GetSystemByHid(newHid);
        if (existingSystem != null)
            throw new PKError($"Another system already exists with ID `{newHid}`.");

        if (!await ctx.PromptYesNo($"Change system ID of `{target.Hid}` to `{newHid}`?", "Change"))
            throw new PKError("ID change cancelled.");

        await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { Hid = newHid });
        await ctx.Reply($"{Emojis.Success} System ID updated (`{target.Hid}` -> `{newHid}`).");
    }

    public async Task UpdateMemberId(Context ctx)
    {
        ctx.AssertBotAdmin();

        var target = await ctx.MatchMember();
        if (target == null)
            throw new PKError("Unknown member.");

        var input = ctx.PopArgument();
        if (!input.TryParseHid(out var newHid))
            throw new PKError($"Invalid new member ID `{input}`.");

        var existingMember = await ctx.Repository.GetMemberByHid(newHid);
        if (existingMember != null)
            throw new PKError($"Another member already exists with ID `{newHid}`.");

        if (!await ctx.PromptYesNo(
            $"Change member ID of **{target.NameFor(LookupContext.ByNonOwner)}** (`{target.Hid}`) to `{newHid}`?",
            "Change"
        ))
            throw new PKError("ID change cancelled.");

        await ctx.Repository.UpdateMember(target.Id, new MemberPatch { Hid = newHid });
        await ctx.Reply($"{Emojis.Success} Member ID updated (`{target.Hid}` -> `{newHid}`).");
    }

    public async Task UpdateGroupId(Context ctx)
    {
        ctx.AssertBotAdmin();

        var target = await ctx.MatchGroup();
        if (target == null)
            throw new PKError("Unknown group.");

        var input = ctx.PopArgument();
        if (!input.TryParseHid(out var newHid))
            throw new PKError($"Invalid new group ID `{input}`.");

        var existingGroup = await ctx.Repository.GetGroupByHid(newHid);
        if (existingGroup != null)
            throw new PKError($"Another group already exists with ID `{newHid}`.");

        if (!await ctx.PromptYesNo($"Change group ID of **{target.Name}** (`{target.Hid}`) to `{newHid}`?",
            "Change"
        ))
            throw new PKError("ID change cancelled.");

        await ctx.Repository.UpdateGroup(target.Id, new GroupPatch { Hid = newHid });
        await ctx.Reply($"{Emojis.Success} Group ID updated (`{target.Hid}` -> `{newHid}`).");
    }

    public async Task RerollSystemId(Context ctx)
    {
        ctx.AssertBotAdmin();

        var target = await ctx.MatchSystem();
        if (target == null)
            throw new PKError("Unknown system.");

        if (!await ctx.PromptYesNo($"Reroll system ID `{target.Hid}`?", "Reroll"))
            throw new PKError("ID change cancelled.");

        var query = new Query("systems").AsUpdate(new
        {
            hid = new UnsafeLiteral("find_free_system_hid()"),
        })
        .Where("id", target.Id);

        var newHid = await ctx.Database.QueryFirst<string>(query, "returning hid");
        await ctx.Reply($"{Emojis.Success} System ID updated (`{target.Hid}` -> `{newHid}`).");
    }

    public async Task RerollMemberId(Context ctx)
    {
        ctx.AssertBotAdmin();

        var target = await ctx.MatchMember();
        if (target == null)
            throw new PKError("Unknown member.");

        if (!await ctx.PromptYesNo(
            $"Reroll member ID for **{target.NameFor(LookupContext.ByNonOwner)}** (`{target.Hid}`)?",
            "Reroll"
        ))
            throw new PKError("ID change cancelled.");

        var query = new Query("members").AsUpdate(new
        {
            hid = new UnsafeLiteral("find_free_member_hid()"),
        })
        .Where("id", target.Id);

        var newHid = await ctx.Database.QueryFirst<string>(query, "returning hid");
        await ctx.Reply($"{Emojis.Success} Member ID updated (`{target.Hid}` -> `{newHid}`).");
    }

    public async Task RerollGroupId(Context ctx)
    {
        ctx.AssertBotAdmin();

        var target = await ctx.MatchGroup();
        if (target == null)
            throw new PKError("Unknown group.");

        if (!await ctx.PromptYesNo($"Reroll group ID for **{target.Name}** (`{target.Hid}`)?",
            "Change"
        ))
            throw new PKError("ID change cancelled.");

        var query = new Query("groups").AsUpdate(new
        {
            hid = new UnsafeLiteral("find_free_group_hid()"),
        })
        .Where("id", target.Id);

        var newHid = await ctx.Database.QueryFirst<string>(query, "returning hid");
        await ctx.Reply($"{Emojis.Success} Group ID updated (`{target.Hid}` -> `{newHid}`).");
    }

    public async Task SystemMemberLimit(Context ctx)
    {
        ctx.AssertBotAdmin();

        var target = await ctx.MatchSystem();
        if (target == null)
            throw new PKError("Unknown system.");

        var config = await ctx.Repository.GetSystemConfig(target.Id);

        var currentLimit = config.MemberLimitOverride ?? Limits.MaxMemberCount;
        if (!ctx.HasNext())
        {
            await ctx.Reply($"Current member limit is **{currentLimit}** members.");
            return;
        }

        var newLimitStr = ctx.PopArgument();
        if (!int.TryParse(newLimitStr, out var newLimit))
            throw new PKError($"Couldn't parse `{newLimitStr}` as number.");

        if (!await ctx.PromptYesNo($"Update member limit from **{currentLimit}** to **{newLimit}**?", "Update"))
            throw new PKError("Member limit change cancelled.");

        await ctx.Repository.UpdateSystemConfig(target.Id, new SystemConfigPatch { MemberLimitOverride = newLimit });
        await ctx.Reply($"{Emojis.Success} Member limit updated.");
    }

    public async Task SystemGroupLimit(Context ctx)
    {
        ctx.AssertBotAdmin();

        var target = await ctx.MatchSystem();
        if (target == null)
            throw new PKError("Unknown system.");

        var config = await ctx.Repository.GetSystemConfig(target.Id);

        var currentLimit = config.GroupLimitOverride ?? Limits.MaxGroupCount;
        if (!ctx.HasNext())
        {
            await ctx.Reply($"Current group limit is **{currentLimit}** groups.");
            return;
        }

        var newLimitStr = ctx.PopArgument();
        if (!int.TryParse(newLimitStr, out var newLimit))
            throw new PKError($"Couldn't parse `{newLimitStr}` as number.");

        if (!await ctx.PromptYesNo($"Update group limit from **{currentLimit}** to **{newLimit}**?", "Update"))
            throw new PKError("Group limit change cancelled.");

        await ctx.Repository.UpdateSystemConfig(target.Id, new SystemConfigPatch { GroupLimitOverride = newLimit });
        await ctx.Reply($"{Emojis.Success} Group limit updated.");
    }

    public async Task SystemRecover(Context ctx)
    {
        ctx.AssertBotAdmin();

        var rerollToken = ctx.MatchFlag("rt", "reroll-token");

        var systemToken = ctx.PopArgument();
        var systemId = await ctx.Database.Execute(conn => conn.QuerySingleOrDefaultAsync<SystemId?>(
             "select id from systems where token = @token",
            new { token = systemToken }
        ));

        if (systemId == null)
            throw new PKError("Could not retrieve a system with that token.");

        var account = await ctx.MatchUser();
        if (account == null)
            throw new PKError("You must pass an account to associate the system with (either ID or @mention).");

        var existingAccount = await ctx.Repository.GetSystemByAccount(account.Id);
        if (existingAccount != null)
            throw Errors.AccountInOtherSystem(existingAccount, ctx.Config);

        var system = await ctx.Repository.GetSystem(systemId.Value!);

        if (!await ctx.PromptYesNo($"Associate account {account.NameAndMention()} with system `{system.Hid}`?", "Recover account"))
            throw new PKError("System recovery cancelled.");

        await ctx.Repository.AddAccount(system.Id, account.Id);
        if (rerollToken)
            await ctx.Repository.UpdateSystem(system.Id, new SystemPatch { Token = StringUtils.GenerateToken() });

        if ((await ctx.BotPermissions).HasFlag(PermissionSet.ManageMessages))
            await _rest.DeleteMessage(ctx.Message);

        await ctx.Reply(null, new Embed
        {
            Title = "System recovered",
            Description = $"{account.NameAndMention()} has been linked to system `{system.Hid}`.",
            Fields = new Embed.Field[]
            {
                new Embed.Field("Token rerolled?", rerollToken ? "yes" : "no", true),
                new Embed.Field("Actioned by", ctx.Author.NameAndMention(), true),
            },
            Color = DiscordUtils.Green,
        });
    }
}