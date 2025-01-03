use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    [command!(
        [Token::cmd("help")],
        "help",
        "Shows the help command"
    )]
    .into_iter()
}
