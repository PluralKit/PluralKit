use std::iter::once;

use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let server_config = ("serverconfig", ["guildconfig", "scfg", "gcfg"]);

    let log = tokens!(server_config, ("log", ["log", "logging"]));
    let log_channel = tokens!(log, ("channel", ["ch", "chan"]));
    let log_cleanup = tokens!(log, ("cleanup", ["clean"]));
    let log_cleanup_short = tokens!(server_config, ("logclean", ["logclean", "logcleanup"]));
    let log_blacklist = tokens!(log, ("blacklist", ["bl", "ignore"]));

    let proxy = tokens!(server_config, ("proxy", ["proxy", "proxying"]));
    let proxy_blacklist = tokens!(proxy, ("blacklist", ["bl", "ignore", "disable"]));

    let invalid = tokens!(
        server_config,
        ("invalid", ["invalid", "unknown"]),
        ("command", ["command", "cmd"]),
        ("error", ["error", "response"])
    );
    let invalid_short = tokens!(
        server_config,
        (
            "invalidcommanderror",
            ["invalidcommanderror", "unknowncommanderror", "ice"]
        )
    );

    let require_tag = tokens!(
        server_config,
        ("require", ["require", "enforce"]),
        ("tag", ["tag", "systemtag"])
    );
    let require_tag_short = tokens!(server_config, ("requiretag", ["requiretag", "enforcetag"]));

    let suppress = tokens!(
        server_config,
        ("suppress", ["suppress"]),
        ("notifications", ["notifications", "notifs"])
    );
    let suppress_short = tokens!(server_config, ("proxysilent", ["proxysilent", "silent"]));

    // Common tokens for add/remove operations
    let add = ("add", ["enable", "on", "deny"]);
    let remove = ("remove", ["disable", "off", "allow"]);

    let log_channel_cmds = [
        command!(log_channel => "server_config_log_channel_show")
            .help("Shows the current log channel"),
        command!(log_channel, ("channel", ChannelRef) => "server_config_log_channel_set")
            .help("Sets the log channel"),
        command!(log_channel, CLEAR => "server_config_log_channel_clear")
            .flag(YES)
            .help("Clears the log channel"),
    ];

    let log_cleanup_cmds = [
        command!(log_cleanup => "server_config_log_cleanup_show")
            .help("Shows whether log cleanup is enabled"),
        command!(log_cleanup, Toggle => "server_config_log_cleanup_set")
            .help("Enables or disables log cleanup"),
        command!(log_cleanup_short => "server_config_log_cleanup_show")
            .help("Shows whether log cleanup is enabled"),
        command!(log_cleanup_short, Toggle => "server_config_log_cleanup_set")
            .help("Enables or disables log cleanup"),
    ];

    let log_blacklist_cmds = [
        command!(log_blacklist => "server_config_log_blacklist_show")
            .help("Shows channels where logging is disabled"),
        command!(log_blacklist, add, Optional(("channel", ChannelRef)) => "server_config_log_blacklist_add")
            .flag(ALL)
            .help("Adds a channel (or all channels with --all) to the log blacklist"),
        command!(log_blacklist, remove, Optional(("channel", ChannelRef)) => "server_config_log_blacklist_remove")
            .flag(ALL)
            .help("Removes a channel (or all channels with --all) from the log blacklist"),
    ];

    let proxy_blacklist_cmds = [
        command!(proxy_blacklist => "server_config_proxy_blacklist_show")
            .help("Shows channels where proxying is disabled"),
        command!(proxy_blacklist, add, Optional(("channel", ChannelRef)) => "server_config_proxy_blacklist_add")
            .flag(ALL)
            .help("Adds a channel (or all channels with --all) to the proxy blacklist"),
        command!(proxy_blacklist, remove, Optional(("channel", ChannelRef)) => "server_config_proxy_blacklist_remove")
            .flag(ALL)
            .help("Removes a channel (or all channels with --all) from the proxy blacklist"),
    ];

    let invalid_cmds = [
        command!(invalid => "server_config_invalid_command_response_show")
            .help("Shows whether error responses for invalid commands are enabled"),
        command!(invalid, Toggle => "server_config_invalid_command_response_set")
            .help("Enables or disables error responses for invalid commands"),
        command!(invalid_short => "server_config_invalid_command_response_show")
            .help("Shows whether error responses for invalid commands are enabled"),
        command!(invalid_short, Toggle => "server_config_invalid_command_response_set")
            .help("Enables or disables error responses for invalid commands"),
    ];

    let require_tag_cmds = [
        command!(require_tag => "server_config_require_system_tag_show")
            .help("Shows whether system tags are required"),
        command!(require_tag, Toggle => "server_config_require_system_tag_set")
            .help("Requires or unrequires system tags for proxied messages"),
        command!(require_tag_short => "server_config_require_system_tag_show")
            .help("Shows whether system tags are required"),
        command!(require_tag_short, Toggle => "server_config_require_system_tag_set")
            .help("Requires or unrequires system tags for proxied messages"),
    ];

    let suppress_cmds = [
        command!(suppress => "server_config_suppress_notifications_show")
            .help("Shows whether notifications are suppressed for proxied messages"),
        command!(suppress, Toggle => "server_config_suppress_notifications_set")
            .help("Enables or disables notification suppression for proxied messages"),
        command!(suppress_short => "server_config_suppress_notifications_show")
            .help("Shows whether notifications are suppressed for proxied messages"),
        command!(suppress_short, Toggle => "server_config_suppress_notifications_set")
            .help("Enables or disables notification suppression for proxied messages"),
    ];

    let main_cmd = once(
        command!(server_config => "server_config_show")
            .help("Shows the current server configuration"),
    );

    main_cmd
        .chain(log_channel_cmds)
        .chain(log_cleanup_cmds)
        .chain(log_blacklist_cmds)
        .chain(proxy_blacklist_cmds)
        .chain(invalid_cmds)
        .chain(require_tag_cmds)
        .chain(suppress_cmds)
}
