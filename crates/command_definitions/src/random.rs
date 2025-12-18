use crate::utils::get_list_flags;

use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let random = ("random", ["rand"]);
    let group = group::group();

    [
        command!(random => "random_self").flag(group),
        command!(random, group => "random_group_self"),
        command!(random, group::targeted() => "random_group_member_self").flags(get_list_flags()),
        command!(system::targeted(), random => "system_random").flag(group),
        command!(system::targeted(), random, group => "system_random_group"),
        command!(group::targeted(), random => "group_random_member").flags(get_list_flags()),
    ]
    .into_iter()
    .map(|cmd| cmd.flag(ALL))
}
