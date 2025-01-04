use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    [command!(
        ["thunder"],
        "fun_thunder",
        "fun thunder"
    )]
    .into_iter()
}
