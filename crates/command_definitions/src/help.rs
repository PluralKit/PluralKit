use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let help = ("help", ["h"]);
    [
        command!("explain" => "explain"),
        command!(help => "help")
            .flag(("foo", OpaqueString)) // todo: just for testing
            .help("Shows the help command"),
        command!(help, "commands" => "help_commands").help("help commands"),
        command!(help, "proxy" => "help_proxy").help("help proxy"),
    ]
    .into_iter()
}
