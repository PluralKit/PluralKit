use command_parser::parameter::MESSAGE_REF;

use super::*;

pub fn debug() -> (&'static str, [&'static str; 1]) {
    ("debug", ["dbg"])
}

pub fn cmds() -> impl IntoIterator<Item = Command> {
    let debug = debug();
    let perms = ("permissions", ["perms", "permcheck"]);
    [
        command!(debug, perms, ("channel", ["ch"]), ChannelRef => "permcheck_channel")
            .help("Checks if PluralKit has the required permissions in a channel"),
        command!(debug, perms, ("guild", ["g"]), GuildRef => "permcheck_guild")
            .help("Checks whether a server's permission setup is correct"),
        command!(debug, ("proxy", ["proxying", "proxycheck"]), MESSAGE_REF => "message_proxy_check")
            .help("Checks why a message has not been proxied"),
    ]
}
