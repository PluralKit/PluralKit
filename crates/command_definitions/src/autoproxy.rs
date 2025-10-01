use super::*;

pub fn autoproxy() -> (&'static str, [&'static str; 2]) {
    ("autoproxy", ["ap", "auto"])
}

pub fn cmds() -> impl Iterator<Item = Command> {
    let ap = autoproxy();

    [
        command!(ap => "autoproxy_show").help("Shows your current autoproxy settings"),
        command!(ap, ("off", ["stop", "cancel", "no", "disable", "remove"]) => "autoproxy_off")
            .help("Disables autoproxy"),
        command!(ap, ("latch", ["last", "proxy", "stick", "sticky", "l"]) => "autoproxy_latch")
            .help("Sets autoproxy to latch mode"),
        command!(ap, ("front", ["fronter", "switch", "f"]) => "autoproxy_front")
            .help("Sets autoproxy to front mode"),
        command!(ap, MemberRef => "autoproxy_member").help("Sets autoproxy to a specific member"),
    ]
    .into_iter()
}
