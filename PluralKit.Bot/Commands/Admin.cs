using System.Text.RegularExpressions;

using Humanizer;
using Dapper;
using SqlKata;

using Myriad.Builders;
using Myriad.Extensions;
using Myriad.Cache;
using Myriad.Rest;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Admin
{
    private readonly BotConfig _botConfig;
    private readonly DiscordApiClient _rest;
    private readonly IDiscordCache _cache;

    public Admin(BotConfig botConfig, DiscordApiClient rest, IDiscordCache cache)
    {
        _botConfig = botConfig;
        _rest = rest;
        _cache = cache;
    }

    private Task<(ulong Id, User? User)[]> GetUsers(IEnumerable<ulong> ids)
    {
        async Task<(ulong Id, User? User)> Inner(ulong id)
        {
            var user = await _cache.GetOrFetchUser(_rest, id);
            return (id, user);
        }

        return Task.WhenAll(ids.Select(Inner));
    }

    public async Task<Embed> CreateEmbed(Context ctx, PKSystem system)
    {
        string UntilLimit(int count, int limit)
        {
            var brackets = new List<int> { 10, 25, 50, 100 };
            if (count == limit)
                return "(at limit)";

            foreach (var x in brackets)
            {
                if (limit - x <= count)
                    return $"(approx. {x} to limit)";
            }

            return "";
        }

        var config = await ctx.Repository.GetSystemConfig(system.Id);

        // Fetch/render info for all accounts simultaneously
        var accounts = await ctx.Repository.GetSystemAccounts(system.Id);
        var users = (await GetUsers(accounts)).Select(x => x.User?.NameAndMention() ?? $"(deleted: `{x.Id}`)");

        var eb = new EmbedBuilder()
            .Title("System info")
            .Color(DiscordUtils.Green)
            .Field(new Embed.Field("System ID", $"`{system.Hid}`"))
            .Field(new Embed.Field("Linked accounts", string.Join("\n", users).Truncate(1000)));

        var memberLimit = config.MemberLimitOverride ?? Limits.MaxMemberCount;
        var memberCount = await ctx.Repository.GetSystemMemberCount(system.Id);
        eb.Field(new Embed.Field("Member limit", $"{memberLimit} {UntilLimit(memberCount, memberLimit)}", true));

        var groupLimit = config.GroupLimitOverride ?? Limits.MaxGroupCount;
        var groupCount = await ctx.Repository.GetSystemGroupCount(system.Id);
        eb.Field(new Embed.Field("Group limit", $"{groupLimit} {UntilLimit(groupCount, groupLimit)}", true));

        return eb.Build();
    }

    public async Task<Embed> CreateAbuseLogEmbed(Context ctx, AbuseLog abuseLog)
    {
        // Fetch/render info for all accounts simultaneously
        var accounts = await ctx.Repository.GetAbuseLogAccounts(abuseLog.Id);
        var systems = await Task.WhenAll(accounts.Select(x => ctx.Repository.GetSystemByAccount(x)));
        var users = (await GetUsers(accounts)).Select(x => x.User?.NameAndMention() ?? $"(deleted: `{x.Id}`)");

        List<string> flagstr = new();
        if (abuseLog.DenyBotUsage)
            flagstr.Add("- bot usage denied");

        var eb = new EmbedBuilder()
            .Title($"Abuse log: {abuseLog.Uuid.ToString()}")
            .Color(DiscordUtils.Red)
            .Footer(new Embed.EmbedFooter($"Created on {abuseLog.Created.FormatZoned(ctx.Zone)}"));

        if (systems.Any(x => x != null))
        {
            var sysList = string.Join(", ", systems.Select(x => $"`{x.DisplayHid()}`"));
            eb.Field(new Embed.Field($"{Emojis.Warn} Accounts have registered system(s)", sysList));
        }

        eb.Field(new Embed.Field("Accounts", string.Join("\n", users).Truncate(1000), true));
        eb.Field(new Embed.Field("Flags", flagstr.Any() ? string.Join("\n", flagstr) : "(none)", true));

        if (abuseLog.Description != null)
            eb.Field(new Embed.Field("Description", abuseLog.Description.Truncate(1000)));

        return eb.Build();
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

        await ctx.Reply(null, await CreateEmbed(ctx, target));

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

        var system = await ctx.Repository.GetSystem(target.System);
        await ctx.Reply(null, await CreateEmbed(ctx, system));

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

        var system = await ctx.Repository.GetSystem(target.System);
        await ctx.Reply(null, await CreateEmbed(ctx, system));

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

        await ctx.Reply(null, await CreateEmbed(ctx, target));

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

        var system = await ctx.Repository.GetSystem(target.System);
        await ctx.Reply(null, await CreateEmbed(ctx, system));

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

        var system = await ctx.Repository.GetSystem(target.System);
        await ctx.Reply(null, await CreateEmbed(ctx, system));

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
            await ctx.Reply(null, await CreateEmbed(ctx, target));
            return;
        }

        var newLimitStr = ctx.PopArgument();
        if (!int.TryParse(newLimitStr, out var newLimit))
            throw new PKError($"Couldn't parse `{newLimitStr}` as number.");

        await ctx.Reply(null, await CreateEmbed(ctx, target));
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
            await ctx.Reply(null, await CreateEmbed(ctx, target));
            return;
        }

        var newLimitStr = ctx.PopArgument();
        if (!int.TryParse(newLimitStr, out var newLimit))
            throw new PKError($"Couldn't parse `{newLimitStr}` as number.");

        await ctx.Reply(null, await CreateEmbed(ctx, target));
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
        await ctx.Reply(null, await CreateEmbed(ctx, system));

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

    public async Task SystemDelete(Context ctx)
    {
        ctx.AssertBotAdmin();

        var target = await ctx.MatchSystem();
        if (target == null)
            throw new PKError("Unknown system.");

        await ctx.Reply($"To delete the following system, reply with the system's UUID: `{target.Uuid.ToString()}`",
            await CreateEmbed(ctx, target));
        if (!await ctx.ConfirmWithReply(target.Uuid.ToString()))
            throw new PKError("System deletion cancelled.");

        await ctx.BusyIndicator(async () =>
            await ctx.Repository.DeleteSystem(target.Id));
        await ctx.Reply($"{Emojis.Success} System deletion succesful.");
    }

    public async Task AbuseLogCreate(Context ctx)
    {
        var denyBotUsage = ctx.MatchFlag("deny", "deny-bot-usage");
        var account = await ctx.MatchUser();
        if (account == null)
            throw new PKError("You must pass an account to associate the abuse log with (either ID or @mention).");

        string? desc = null!;
        if (ctx.HasNext(false))
            desc = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();

        var abuseLog = await ctx.Repository.CreateAbuseLog(desc, denyBotUsage);
        await ctx.Repository.AddAbuseLogAccount(abuseLog.Id, account.Id);

        await ctx.Reply(
            $"Created new abuse log with UUID `{abuseLog.Uuid.ToString()}`.",
            await CreateAbuseLogEmbed(ctx, abuseLog));
    }

    public async Task AbuseLogShow(Context ctx, AbuseLog abuseLog)
    {
        await ctx.Reply(null, await CreateAbuseLogEmbed(ctx, abuseLog));
    }

    public async Task AbuseLogFlagDeny(Context ctx, AbuseLog abuseLog)
    {
        if (!ctx.HasNext())
        {
            await ctx.Reply(
                $"Bot usage is currently {(abuseLog.DenyBotUsage ? "denied" : "allowed")} "
                + $"for accounts associated with abuse log `{abuseLog.Uuid}`.");
        }
        else
        {
            var value = ctx.MatchToggle(true);
            if (abuseLog.DenyBotUsage != value)
                await ctx.Repository.UpdateAbuseLog(abuseLog.Id, new AbuseLogPatch { DenyBotUsage = value });

            await ctx.Reply(
                $"Bot usage is now **{(value ? "denied" : "allowed")}** "
                + $"for accounts associated with abuse log `{abuseLog.Uuid}`.");
        }
    }

    public async Task AbuseLogDescription(Context ctx, AbuseLog abuseLog)
    {
        if (ctx.MatchClear() && await ctx.ConfirmClear("this abuse log description"))
        {
            await ctx.Repository.UpdateAbuseLog(abuseLog.Id, new AbuseLogPatch { Description = null });
            await ctx.Reply($"{Emojis.Success} Abuse log description cleared.");
        }
        else if (ctx.HasNext())
        {
            var desc = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();
            await ctx.Repository.UpdateAbuseLog(abuseLog.Id, new AbuseLogPatch { Description = desc });
            await ctx.Reply($"{Emojis.Success} Abuse log description updated.");
        }
        else
        {
            var eb = new EmbedBuilder()
                .Description($"Showing description for abuse log `{abuseLog.Uuid}`");
            await ctx.Reply(abuseLog.Description, eb.Build());
        }
    }

    public async Task AbuseLogAddUser(Context ctx, AbuseLog abuseLog)
    {
        var account = await ctx.MatchUser();
        if (account == null)
            throw new PKError("You must pass an account to associate the abuse log with (either ID or @mention).");

        await ctx.Repository.AddAbuseLogAccount(abuseLog.Id, account.Id);
        await ctx.Reply(
            $"Added user {account.NameAndMention()} to the abuse log with UUID `{abuseLog.Uuid.ToString()}`.",
            await CreateAbuseLogEmbed(ctx, abuseLog));
    }

    public async Task AbuseLogRemoveUser(Context ctx, AbuseLog abuseLog)
    {
        var account = await ctx.MatchUser();
        if (account == null)
            throw new PKError("You must pass an account to remove from the abuse log (either ID or @mention).");

        await ctx.Repository.UpdateAccount(account.Id, new()
        {
            AbuseLog = null,
        });

        await ctx.Reply(
            $"Removed user {account.NameAndMention()} from the abuse log with UUID `{abuseLog.Uuid.ToString()}`.",
            await CreateAbuseLogEmbed(ctx, abuseLog));
    }

    public async Task AbuseLogDelete(Context ctx, AbuseLog abuseLog)
    {
        if (!await ctx.PromptYesNo($"Really delete abuse log entry `{abuseLog.Uuid}`?", "Delete", matchFlag: false))
        {
            await ctx.Reply($"{Emojis.Error} Deletion cancelled.");
            return;
        }

        await ctx.Repository.DeleteAbuseLog(abuseLog.Id);
        await ctx.Reply($"{Emojis.Success} Successfully deleted abuse log entry.");
    }
}