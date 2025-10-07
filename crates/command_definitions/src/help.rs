use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let help = ("help", ["h"]);
    [
        command!(("commands", ["cmd", "c"]), ("subject", OpaqueString) => "commands_list"),
        command!(("dashboard", ["dash"]) => "dashboard"),
        command!("explain" => "explain"),
        command!(help => "help").help("Shows the help command"),
        command!(help, "commands" => "help_commands").help("help commands"),
        command!(help, "proxy" => "help_proxy").help("help proxy"),
    ]
    .into_iter()
}
