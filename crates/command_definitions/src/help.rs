use super::*;

pub fn cmds() -> impl IntoIterator<Item = Command> {
    let help = ("help", ["h"]);
    [
        command!(("commands", ["cmd", "c"]), ("subject", OpaqueString) => "commands_list")
            .help("Lists all commands or commands in a specific category"),
        command!(("dashboard", ["dash"]) => "dashboard")
            .help("Gets a link to the PluralKit web dashboard"),
        command!("explain" => "explain").help("Explains the basics of systems and proxying"),
        command!(help => "help").help("Shows the help command"),
        command!(help, "commands" => "help_commands").help("help commands"),
        command!(help, "proxy" => "help_proxy").help("help proxy"),
    ]
}
