use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    [command!(
        [Token::cmd("thunder")],
        "fun_thunder",
        "Shows the help command"
    )]
    .into_iter()
}
