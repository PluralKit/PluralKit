use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let help = ["help", "h"];
    [
        command!([help], "help", "Shows the help command"),
        command!([help, "commands"], "help_commands", "help commands"),
        command!([help, "proxy"], "help_proxy", "help proxy"),
    ]
    .into_iter()
}
