use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    use Token::*;

    let system = ["system", "s"];
    let new = ["new", "n"];

    [
        command!(
            [system],
            "system_show",
            "Shows information about your system"
        ),
        command!([system, new], "system_new", "Creates a new system"),
        command!(
            [system, new, FullString("name")],
            "system_new",
            "Creates a new system"
        ),
    ]
    .into_iter()
}
