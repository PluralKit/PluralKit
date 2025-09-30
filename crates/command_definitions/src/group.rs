use command_parser::token::TokensIterator;

use crate::utils::get_list_flags;

use super::*;

pub fn group() -> (&'static str, [&'static str; 2]) {
    ("group", ["g", "groups"])
}

pub fn targeted() -> TokensIterator {
    tokens!(group(), GroupRef)
}

pub fn cmds() -> impl Iterator<Item = Command> {
    let group = group();
    let group_target = targeted();

    let apply_list_opts = |cmd: Command| cmd.flags(get_list_flags());

    let group_list_members = tokens!(group_target, ("members", ["list", "ls"]));
    let group_list_members_cmd = [
        command!(group_list_members => "group_list_members"),
        command!(group_list_members, "list" => "group_list_members"),
        command!(group_list_members, ("search", ["find", "query"]), ("query", OpaqueStringRemainder) => "group_search_members"),
    ]
    .into_iter()
    .map(apply_list_opts);

    let system_groups_cmd = [
        command!(group, ("list", ["ls"]) => "group_list_groups"),
        command!(group, ("search", ["find", "query"]), ("query", OpaqueStringRemainder) => "group_search_groups"),
    ]
    .into_iter()
    .map(apply_list_opts);

    system_groups_cmd.chain(group_list_members_cmd)
}
