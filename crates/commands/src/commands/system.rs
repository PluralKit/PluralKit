use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    use Token::*;

    let system = Token::cmd_with_alias(["system", "s"]);
    let new = Token::cmd_with_alias(["new", "n"]);

    [
        command!(
            [system],
            "system_show",
            "Shows information about your system"
        ),
        command!([system, new], "system_new", "Creates a new system"),
        command!(
            [system, new, FullString],
            "system_new",
            "Creates a new system"
        ),
    ]
    .into_iter()
}
