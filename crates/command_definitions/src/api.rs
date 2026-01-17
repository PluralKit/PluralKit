use super::*;

pub fn cmds() -> impl IntoIterator<Item = Command> {
    [
        command!("token" => "token_display"),
        command!("token", ("refresh", ["renew", "regen", "reroll"]) => "token_refresh"),
    ]
}
