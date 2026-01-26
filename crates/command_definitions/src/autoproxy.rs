use super::*;

pub fn autoproxy() -> (&'static str, [&'static str; 2]) {
    ("autoproxy", ["ap", "auto"])
}

pub fn cmds() -> impl IntoIterator<Item = Command> {
    let ap = autoproxy();

    [
        command!(ap => "autoproxy_show").help("Shows your current autoproxy settings"),
        command!(ap, ("off", ["stop", "cancel", "no", "disable", "remove"]) => "autoproxy_off")
            .help("Disables autoproxying for your system in the current server"),
        command!(ap, ("latch", ["last", "proxy", "stick", "sticky", "l"]) => "autoproxy_latch")
            .help("Sets your system's autoproxy in this server to proxy the last manually proxied member"),
        command!(ap, ("front", ["fronter", "switch", "f"]) => "autoproxy_front")
            .help("Sets your system's autoproxy in this server to proxy the first member currently registered as front"),
        command!(ap, MemberRef => "autoproxy_member").help("Sets your system's autoproxy in this server to proxy a specific member"),
    ]
}
