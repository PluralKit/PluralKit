using Humanizer;
using Dapper;
using SqlKata;

using Myriad.Builders;
using Myriad.Extensions;
using Myriad.Cache;
using Myriad.Rest;
using Myriad.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Rest.Exceptions;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Admin
{
    private readonly BotConfig _botConfig;
    private readonly DiscordApiClient _rest;
    private readonly IDiscordCache _cache;
    private readonly PrivateChannelService _dmCache;

    public Admin(BotConfig botConfig, DiscordApiClient rest, IDiscordCache cache, PrivateChannelService dmCache)
    {
        _botConfig = botConfig;
        _rest = rest;
        _cache = cache;
        _dmCache = dmCache;
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

    public async Task UpdateSystemId(Context ctx, PKSystem target, string newHid, bool confirmYes)
    {
        ctx.AssertBotAdmin();

        var existingSystem = await ctx.Repository.GetSystemByHid(newHid);
        if (existingSystem != null)
            throw new PKError($"Another system already exists with ID `{newHid}`.");

        await ctx.Reply(null, await CreateEmbed(ctx, target));

        if (!await ctx.PromptYesNo($"Change system ID of `{target.Hid}` to `{newHid}`?", "Change", flagValue: confirmYes))
            throw new PKError("ID change cancelled.");

        await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { Hid = newHid });
        await ctx.Reply($"{Emojis.Success} System ID updated (`{target.Hid}` -> `{newHid}`).");
    }

    public async Task UpdateMemberId(Context ctx, PKMember target, string newHid, bool confirmYes)
    {
        ctx.AssertBotAdmin();

        var existingMember = await ctx.Repository.GetMemberByHid(newHid);
        if (existingMember != null)
            throw new PKError($"Another member already exists with ID `{newHid}`.");

        var system = await ctx.Repository.GetSystem(target.System);
        await ctx.Reply(null, await CreateEmbed(ctx, system));

        if (!await ctx.PromptYesNo(
            $"Change member ID of **{target.NameFor(LookupContext.ByNonOwner)}** (`{target.Hid}`) to `{newHid}`?",
            "Change", flagValue: confirmYes
        ))
            throw new PKError("ID change cancelled.");

        await ctx.Repository.UpdateMember(target.Id, new MemberPatch { Hid = newHid });
        await ctx.Reply($"{Emojis.Success} Member ID updated (`{target.Hid}` -> `{newHid}`).");
    }

    public async Task UpdateGroupId(Context ctx, PKGroup target, string newHid, bool confirmYes)
    {
        ctx.AssertBotAdmin();

        var existingGroup = await ctx.Repository.GetGroupByHid(newHid);
        if (existingGroup != null)
            throw new PKError($"Another group already exists with ID `{newHid}`.");

        var system = await ctx.Repository.GetSystem(target.System);
        await ctx.Reply(null, await CreateEmbed(ctx, system));

        if (!await ctx.PromptYesNo($"Change group ID of **{target.Name}** (`{target.Hid}`) to `{newHid}`?",
            "Change", flagValue: confirmYes
        ))
            throw new PKError("ID change cancelled.");

        await ctx.Repository.UpdateGroup(target.Id, new GroupPatch { Hid = newHid });
        await ctx.Reply($"{Emojis.Success} Group ID updated (`{target.Hid}` -> `{newHid}`).");
    }

    public async Task RerollSystemId(Context ctx, PKSystem target, bool confirmYes)
    {
        ctx.AssertBotAdmin();

        await ctx.Reply(null, await CreateEmbed(ctx, target));

        if (!await ctx.PromptYesNo($"Reroll system ID `{target.Hid}`?", "Reroll", flagValue: confirmYes))
            throw new PKError("ID change cancelled.");

        var query = new Query("systems").AsUpdate(new
        {
            hid = new UnsafeLiteral("find_free_system_hid()"),
        })
        .Where("id", target.Id);

        var newHid = await ctx.Database.QueryFirst<string>(query, "returning hid");
        await ctx.Reply($"{Emojis.Success} System ID updated (`{target.Hid}` -> `{newHid}`).");
    }

    public async Task RerollMemberId(Context ctx, PKMember target, bool confirmYes)
    {
        ctx.AssertBotAdmin();

        var system = await ctx.Repository.GetSystem(target.System);
        await ctx.Reply(null, await CreateEmbed(ctx, system));

        if (!await ctx.PromptYesNo(
            $"Reroll member ID for **{target.NameFor(LookupContext.ByNonOwner)}** (`{target.Hid}`)?",
            "Reroll", flagValue: confirmYes
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

    public async Task RerollGroupId(Context ctx, PKGroup target, bool confirmYes)
    {
        ctx.AssertBotAdmin();

        var system = await ctx.Repository.GetSystem(target.System);
        await ctx.Reply(null, await CreateEmbed(ctx, system));

        if (!await ctx.PromptYesNo($"Reroll group ID for **{target.Name}** (`{target.Hid}`)?",
            "Change", flagValue: confirmYes
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

    public async Task SystemMemberLimit(Context ctx, PKSystem target, int? newLimit, bool confirmYes)
    {
        ctx.AssertBotAdmin();

        var config = await ctx.Repository.GetSystemConfig(target.Id);

        var currentLimit = config.MemberLimitOverride ?? Limits.MaxMemberCount;
        if (newLimit == null)
        {
            await ctx.Reply(null, await CreateEmbed(ctx, target));
            return;
        }

        await ctx.Reply(null, await CreateEmbed(ctx, target));
        if (!await ctx.PromptYesNo($"Update member limit from **{currentLimit}** to **{newLimit}**?", "Update", flagValue: confirmYes))
            throw new PKError("Member limit change cancelled.");

        await ctx.Repository.UpdateSystemConfig(target.Id, new SystemConfigPatch { MemberLimitOverride = newLimit });
        await ctx.Reply($"{Emojis.Success} Member limit updated.");
    }

    public async Task SystemGroupLimit(Context ctx, PKSystem target, int? newLimit, bool confirmYes)
    {
        ctx.AssertBotAdmin();

        var config = await ctx.Repository.GetSystemConfig(target.Id);

        var currentLimit = config.GroupLimitOverride ?? Limits.MaxGroupCount;
        if (newLimit == null)
        {
            await ctx.Reply(null, await CreateEmbed(ctx, target));
            return;
        }

        await ctx.Reply(null, await CreateEmbed(ctx, target));
        if (!await ctx.PromptYesNo($"Update group limit from **{currentLimit}** to **{newLimit}**?", "Update", flagValue: confirmYes))
            throw new PKError("Group limit change cancelled.");

        await ctx.Repository.UpdateSystemConfig(target.Id, new SystemConfigPatch { GroupLimitOverride = newLimit });
        await ctx.Reply($"{Emojis.Success} Group limit updated.");
    }

    public async Task SystemRecover(Context ctx, string systemToken, User account, bool rerollToken, bool confirmYes)
    {
        ctx.AssertBotAdmin();

        var systemId = await ctx.Database.Execute(conn => conn.QuerySingleOrDefaultAsync<SystemId?>(
             "select id from systems where token = @token",
            new { token = systemToken }
        ));

        if (systemId == null)
            throw new PKError("Could not retrieve a system with that token.");

        var existingAccount = await ctx.Repository.GetSystemByAccount(account.Id);
        if (existingAccount != null)
            throw Errors.AccountInOtherSystem(existingAccount, ctx.Config, ctx.DefaultPrefix);

        var system = await ctx.Repository.GetSystem(systemId.Value!);
        await ctx.Reply(null, await CreateEmbed(ctx, system));

        if (!await ctx.PromptYesNo($"Associate account {account.NameAndMention()} with system `{system.Hid}`?", "Recover account", flagValue: confirmYes))
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

    public async Task SystemDelete(Context ctx, PKSystem target)
    {
        ctx.AssertBotAdmin();

        await ctx.Reply($"To delete the following system, reply with the system's UUID: `{target.Uuid.ToString()}`",
            await CreateEmbed(ctx, target));
        if (!await ctx.ConfirmWithReply(target.Uuid.ToString()))
            throw new PKError("System deletion cancelled.");

        await ctx.BusyIndicator(async () =>
            await ctx.Repository.DeleteSystem(target.Id));
        await ctx.Reply($"{Emojis.Success} System deletion succesful.");
    }

    public async Task AbuseLogCreate(Context ctx, User account, bool denyBotUsage, string? description)
    {
        ctx.AssertBotAdmin();

        var abuseLog = await ctx.Repository.CreateAbuseLog(description, denyBotUsage);
        await ctx.Repository.AddAbuseLogAccount(abuseLog.Id, account.Id);

        await ctx.Reply(
            $"Created new abuse log with UUID `{abuseLog.Uuid.ToString()}`.",
            await CreateAbuseLogEmbed(ctx, abuseLog));
    }

    public async Task<AbuseLog?> GetAbuseLog(Context ctx, User? account, string? id)
    {
        ctx.AssertBotAdmin();

        AbuseLog? abuseLog = null!;
        if (account != null)
        {
            abuseLog = await ctx.Repository.GetAbuseLogByAccount(account.Id);
        }
        else
        {
            abuseLog = await ctx.Repository.GetAbuseLogByGuid(new Guid(id));
        }

        if (abuseLog == null)
        {
            await ctx.Reply($"{Emojis.Error} Could not find an existing abuse log entry for that query.");
            return null;
        }

        return abuseLog;
    }

    public async Task AbuseLogShow(Context ctx, User? account, string? id)
    {
        ctx.AssertBotAdmin();

        AbuseLog? abuseLog = await GetAbuseLog(ctx, account, id);
        if (abuseLog == null)
            return;

        await ctx.Reply(null, await CreateAbuseLogEmbed(ctx, abuseLog));
    }

    public async Task AbuseLogFlagDeny(Context ctx, User? account, string? id, bool? value)
    {
        ctx.AssertBotAdmin();

        AbuseLog? abuseLog = await GetAbuseLog(ctx, account, id);
        if (abuseLog == null)
            return;

        if (value == null)
        {
            await ctx.Reply(
                $"Bot usage is currently {(abuseLog.DenyBotUsage ? "denied" : "allowed")} "
                + $"for accounts associated with abuse log `{abuseLog.Uuid}`.");
        }
        else
        {
            if (abuseLog.DenyBotUsage != value)
                await ctx.Repository.UpdateAbuseLog(abuseLog.Id, new AbuseLogPatch { DenyBotUsage = value.Value });

            await ctx.Reply(
                $"Bot usage is now **{(value.Value ? "denied" : "allowed")}** "
                + $"for accounts associated with abuse log `{abuseLog.Uuid}`.");
        }
    }

    public async Task AbuseLogDescription(Context ctx, User? account, string? id, string? description, bool clear, bool confirmClear)
    {
        ctx.AssertBotAdmin();

        AbuseLog? abuseLog = await GetAbuseLog(ctx, account, id);
        if (abuseLog == null)
            return;

        if (clear && await ctx.ConfirmClear("this abuse log description", confirmClear))
        {
            await ctx.Repository.UpdateAbuseLog(abuseLog.Id, new AbuseLogPatch { Description = null });
            await ctx.Reply($"{Emojis.Success} Abuse log description cleared.");
        }
        else if (description != null)
        {
            await ctx.Repository.UpdateAbuseLog(abuseLog.Id, new AbuseLogPatch { Description = description });
            await ctx.Reply($"{Emojis.Success} Abuse log description updated.");
        }
        else
        {
            var eb = new EmbedBuilder()
                .Description($"Showing description for abuse log `{abuseLog.Uuid}`");
            await ctx.Reply(abuseLog.Description, eb.Build());
        }
    }

    public async Task AbuseLogAddUser(Context ctx, User? accountToFind, string? id, User account)
    {
        ctx.AssertBotAdmin();

        AbuseLog? abuseLog = await GetAbuseLog(ctx, accountToFind, id);
        if (abuseLog == null)
            return;

        await ctx.Repository.AddAbuseLogAccount(abuseLog.Id, account.Id);
        await ctx.Reply(
            $"Added user {account.NameAndMention()} to the abuse log with UUID `{abuseLog.Uuid.ToString()}`.",
            await CreateAbuseLogEmbed(ctx, abuseLog));
    }

    public async Task AbuseLogRemoveUser(Context ctx, User? accountToFind, string? id, User account)
    {
        ctx.AssertBotAdmin();

        AbuseLog? abuseLog = await GetAbuseLog(ctx, accountToFind, id);
        if (abuseLog == null)
            return;

        await ctx.Repository.UpdateAccount(account.Id, new()
        {
            AbuseLog = null,
        });

        await ctx.Reply(
            $"Removed user {account.NameAndMention()} from the abuse log with UUID `{abuseLog.Uuid.ToString()}`.",
            await CreateAbuseLogEmbed(ctx, abuseLog));
    }

    public async Task AbuseLogDelete(Context ctx, User? account, string? id)
    {
        ctx.AssertBotAdmin();

        AbuseLog? abuseLog = await GetAbuseLog(ctx, account, id);
        if (abuseLog == null)
            return;

        if (!await ctx.PromptYesNo($"Really delete abuse log entry `{abuseLog.Uuid}`?", "Delete", matchFlag: false))
        {
            await ctx.Reply($"{Emojis.Error} Deletion cancelled.");
            return;
        }

        await ctx.Repository.DeleteAbuseLog(abuseLog.Id);
        await ctx.Reply($"{Emojis.Success} Successfully deleted abuse log entry.");
    }

    public async Task SendAdminMessage(Context ctx, User account, string content)
    {
        ctx.AssertBotAdmin();

        var messageContent = $"## [Admin Message]\n\n{content}\n\nWe cannot read replies sent to this DM. If you wish to contact the staff team, please join the support server (<https://discord.gg/PczBt78>) or send us an email at <legal@pluralkit.me>.";

        try
        {
            var dm = await _dmCache.GetOrCreateDmChannel(account.Id);
            var msg = await ctx.Rest.CreateMessage(dm,
                new MessageRequest { Content = messageContent }
            );
        }
        catch (ForbiddenException)
        {
            await ctx.Reply(
                $"{Emojis.Error} Error while sending DM.");
            return;
        }

        await ctx.Reply($"{Emojis.Success} Successfully sent message.");
    }
}