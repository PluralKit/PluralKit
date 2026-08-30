use super::*;

pub fn cmds() -> impl IntoIterator<Item = Command> {
    [
        command!("token" => "token_display").help("Gets your system's API token"),
        command!("token", ("refresh", ["renew", "regen", "reroll"]) => "token_refresh")
            .help("Generates a new API token and invalidates the old one"),
    ]
}
