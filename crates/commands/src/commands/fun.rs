use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    [
        command!(["thunder"], "fun_thunder", "fun thunder"),
        command!(["meow"], "fun_meow", "fun meow"),
    ]
    .into_iter()
}
