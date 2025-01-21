use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    [
        command!(["thunder"] => "fun_thunder"),
        command!(["meow"] => "fun_meow"),
    ]
    .into_iter()
}
