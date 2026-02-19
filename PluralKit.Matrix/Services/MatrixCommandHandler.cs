using PluralKit.Core;

using Serilog;

namespace PluralKit.Matrix;

public class MatrixCommandHandler
{
    private readonly MatrixApiClient _api;
    private readonly MatrixRepository _repo;
    private readonly ModelRepository _coreRepo;
    private readonly MatrixConfig _config;
    private readonly ILogger _logger;

    public MatrixCommandHandler(MatrixApiClient api, MatrixRepository repo, ModelRepository coreRepo,
        MatrixConfig config, ILogger logger)
    {
        _api = api;
        _repo = repo;
        _coreRepo = coreRepo;
        _config = config;
        _logger = logger.ForContext<MatrixCommandHandler>();
    }

    public async Task HandleCommand(MatrixEvent evt, string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts.Length > 0 ? parts[0].ToLower() : "help";
        var botMxid = $"@{_config.BotLocalpart}:{_config.ServerName}";

        try
        {
            var response = command switch
            {
                "link" => await HandleLink(evt, parts),
                "unlink" => await HandleUnlink(evt),
                "system" or "s" => await HandleSystem(evt),
                "member" or "m" => await HandleMember(evt, parts),
                "autoproxy" or "ap" => await HandleAutoproxy(evt, parts),
                "blacklist" => await HandleBlacklist(evt, parts),
                "help" or "?" => GetHelpText(),
                _ => $"Unknown command: {command}. Type `{_config.Prefix} help` for a list of commands."
            };

            if (response != null)
            {
                var txnId = $"pk_cmd_{evt.EventId}_{Guid.NewGuid():N}";
                await _api.SendMessage(evt.RoomId, botMxid, response, null, txnId);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling command '{Command}' from {Sender}", command, evt.Sender);
            try
            {
                var txnId = $"pk_err_{evt.EventId}_{Guid.NewGuid():N}";
                await _api.SendMessage(evt.RoomId, botMxid,
                    $"An error occurred while processing `{command}`.", null, txnId);
            }
            catch (Exception sendEx)
            {
                _logger.Warning(sendEx, "Failed to send error response for command '{Command}'", command);
            }
        }
    }

    private async Task<string> HandleLink(MatrixEvent evt, string[] parts)
    {
        if (parts.Length < 3)
            return $"Usage: `{_config.Prefix} link <system_id> <token>`\n" +
                   "Links your Matrix account to a PluralKit system. " +
                   "Get your token from the PluralKit bot on Discord with `pk;token`.";

        var systemHid = parts[1];
        var token = parts[2];

        var system = await _coreRepo.GetSystemByHid(systemHid);
        if (system == null)
            return $"System `{systemHid}` not found.";

        var systemToken = system.Token;
        if (systemToken == null)
            return "That system has no API token set. Generate one on Discord with `pk;token`.";

        if (token != systemToken)
            return "Invalid token. Make sure you're using the correct system token.";

        var existing = await _repo.GetAccountSystem(evt.Sender);
        if (existing != null)
            return $"Your Matrix account is already linked to a system. Use `{_config.Prefix} unlink` first.";

        await _repo.LinkAccount(evt.Sender, system.Id);
        return $"Linked your Matrix account to system **{system.Name ?? systemHid}** (`{system.Hid}`).";
    }

    private async Task<string> HandleUnlink(MatrixEvent evt)
    {
        var existing = await _repo.GetAccountSystem(evt.Sender);
        if (existing == null)
            return "Your Matrix account is not linked to any system.";

        await _repo.UnlinkAccount(evt.Sender);
        return "Unlinked your Matrix account from your PluralKit system.";
    }

    private async Task<string> HandleSystem(MatrixEvent evt)
    {
        var systemId = await _repo.GetAccountSystem(evt.Sender);
        if (systemId == null)
            return $"You don't have a system linked. Use `{_config.Prefix} link <system_id> <token>` to link one.";

        var system = await _coreRepo.GetSystem(systemId.Value);
        if (system == null)
            return "System not found (it may have been deleted).";

        var memberCount = await _coreRepo.GetSystemMemberCount(system.Id);
        return $"**System**: {system.Name ?? "(unnamed)"}\n" +
               $"**ID**: `{system.Hid}`\n" +
               $"**Members**: {memberCount}\n" +
               $"**Tag**: {system.Tag ?? "(none)"}";
    }

    private async Task<string> HandleMember(MatrixEvent evt, string[] parts)
    {
        if (parts.Length < 2)
            return $"Usage: `{_config.Prefix} member <member_id>`";

        var memberHid = parts[1];
        var member = await _coreRepo.GetMemberByHid(memberHid);
        if (member == null)
            return $"Member `{memberHid}` not found.";

        // Only show member info if the requester owns the system or the member is public
        var requestorSystem = await _repo.GetAccountSystem(evt.Sender);
        if (member.MemberVisibility != PrivacyLevel.Public
            && (requestorSystem == null || requestorSystem.Value != member.System))
            return $"Member `{memberHid}` not found.";

        var isOwner = requestorSystem != null && requestorSystem.Value == member.System;

        var name = (member.NamePrivacy == PrivacyLevel.Public || isOwner)
            ? member.DisplayName ?? member.Name
            : member.Name;

        var response = $"**Member**: {name}\n" +
                       $"**ID**: `{member.Hid}`\n";

        if (member.ProxyPrivacy == PrivacyLevel.Public || isOwner)
        {
            var tags = string.Join(", ", member.ProxyTags.Select(t => $"`{t.ProxyString}`"));
            response += $"**Proxy tags**: {(tags.Length > 0 ? tags : "(none)")}";
        }

        return response;
    }

    private async Task<string> HandleAutoproxy(MatrixEvent evt, string[] parts)
    {
        var systemId = await _repo.GetAccountSystem(evt.Sender);
        if (systemId == null)
            return $"You don't have a system linked. Use `{_config.Prefix} link <system_id> <token>` to link one.";

        if (parts.Length < 2)
        {
            var current = await _repo.GetAutoproxySettings(systemId.Value, evt.RoomId);
            return $"Current autoproxy mode: **{current.AutoproxyMode.ToString().ToLower()}**\n" +
                   $"Usage: `{_config.Prefix} autoproxy <off|front|latch|member> [member_id]`";
        }

        var mode = parts[1].ToLower() switch
        {
            "off" => (AutoproxyMode?)AutoproxyMode.Off,
            "front" => AutoproxyMode.Front,
            "latch" => AutoproxyMode.Latch,
            "member" => AutoproxyMode.Member,
            _ => null
        };

        if (mode == null)
            return $"Invalid autoproxy mode: {parts[1]}. Valid modes: off, front, latch, member";

        MemberId? memberId = null;
        if (mode == AutoproxyMode.Member)
        {
            if (parts.Length < 3)
                return $"Usage: `{_config.Prefix} autoproxy member <member_id>`";

            var member = await _coreRepo.GetMemberByHid(parts[2]);
            if (member == null)
                return $"Member `{parts[2]}` not found.";
            if (member.System != systemId.Value)
                return $"Member `{parts[2]}` does not belong to your system.";
            memberId = member.Id;
        }

        await _repo.UpdateAutoproxy(systemId.Value, evt.RoomId, new AutoproxyPatch
        {
            AutoproxyMode = mode.Value,
            AutoproxyMember = memberId,
        });

        return $"Autoproxy mode set to **{mode.Value.ToString().ToLower()}**" +
               (memberId != null ? $" (member: `{parts[2]}`)" : "") +
               " for this room.";
    }

    private async Task<string> HandleBlacklist(MatrixEvent evt, string[] parts)
    {
        // Only system-linked users can manage blacklists
        var systemId = await _repo.GetAccountSystem(evt.Sender);
        if (systemId == null)
            return $"You must have a linked system to manage room settings. Use `{_config.Prefix} link` first.";

        if (parts.Length < 2)
            return $"Usage: `{_config.Prefix} blacklist <on|off>`\nDisables/enables proxying in the current room.";

        var enable = parts[1].ToLower() switch
        {
            "on" or "enable" or "true" => (bool?)true,
            "off" or "disable" or "false" => false,
            _ => null
        };

        if (enable == null)
            return "Invalid value. Use `on` or `off`.";

        await _repo.SetRoomBlacklisted(evt.RoomId, enable.Value);
        return enable.Value
            ? "Proxying is now **disabled** in this room."
            : "Proxying is now **enabled** in this room.";
    }

    private string GetHelpText()
    {
        var p = _config.Prefix;
        return $"**PluralKit Matrix** \u2014 Commands:\n" +
               $"- `{p} link <system_id> <token>` \u2014 Link your Matrix account to a PK system\n" +
               $"- `{p} unlink` \u2014 Unlink your Matrix account\n" +
               $"- `{p} system` \u2014 View your system info\n" +
               $"- `{p} member <id>` \u2014 View member info\n" +
               $"- `{p} autoproxy <mode> [member]` \u2014 Set autoproxy (off/front/latch/member)\n" +
               $"- `{p} blacklist <on|off>` \u2014 Disable/enable proxying in this room\n" +
               $"- `{p} help` \u2014 Show this help message";
    }
}
