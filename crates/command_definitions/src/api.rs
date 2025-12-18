use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    [
        command!("token" => "token_display"),
        command!("token", ("refresh", ["renew", "regen", "reroll"]) => "token_refresh"),
    ]
    .into_iter()
}
