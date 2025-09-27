use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let random = ("random", ["rand"]);
    let group = group::group();

    [
        command!(random => "random_self").flag(group),
        command!(system::targeted(), random => "system_random").flag(group),
        command!(group::targeted(), random => "group_random_member"),
    ]
    .into_iter()
    .map(|cmd| cmd.flag(("all", ["a"])))
}
