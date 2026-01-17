use super::*;

pub fn debug() -> (&'static str, [&'static str; 1]) {
    ("debug", ["dbg"])
}

pub fn cmds() -> impl IntoIterator<Item = Command> {
    let debug = debug();
    let perms = ("permissions", ["perms", "permcheck"]);
    [
        command!(debug, perms, ("channel", ["ch"]), ChannelRef => "permcheck_channel"),
        command!(debug, perms, ("guild", ["g"]), GuildRef => "permcheck_guild"),
        command!(debug, ("proxy", ["proxying", "proxycheck"]), MessageRef => "message_proxy_check"),
    ]
}
