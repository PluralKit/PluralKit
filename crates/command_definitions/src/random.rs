use crate::utils::get_list_flags;

use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let random = ("random", ["rand"]);
    let group = group::group();
    let member = member::member();

    [
        command!(random => "random_self")
            .help("Shows the info card of a randomly selected member in your system")
            .flag(group),
        command!(random, member => "random_self"),
        command!(random, group => "random_group_self")
            .help("Shows the info card of a randomly selected group in your system"),
        command!(random, group::targeted() => "random_group_member_self")
            .help("Shows the info card of a randomly selected member in a group in your system")
            .flags(get_list_flags()),
        command!(system::targeted(), random => "system_random")
            .help("Shows the info card of a randomly selected member in a system")
            .flag(group),
        command!(system::targeted(), random, group => "system_random_group")
            .help("Shows the info card of a randomly selected group in a system"),
        command!(group::targeted(), random => "group_random_member")
            .help("Shows the info card of a randomly selected member in a group")
            .flags(get_list_flags()),
    ]
    .into_iter()
    .map(|cmd| cmd.flag(ALL))
}
