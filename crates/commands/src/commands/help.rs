use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let help = ["help", "h"];
    [
        command!(
            [help],
            "help",
            "Shows the help command"
        ),
        command!(
            [help, "commands"],
            "help_commands",
            "Commands"
        ),
    ]
    .into_iter()
}
