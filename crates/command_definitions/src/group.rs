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

    let group_new = tokens!(group, ("new", ["n"]));
    let group_new_cmd =
        [command!(group_new, ("name", OpaqueString) => "group_new").help("Creates a new group")]
            .into_iter();

    let group_info_cmd =
        [command!(group_target => "group_info").help("Shows information about a group")]
            .into_iter();

    let group_name = tokens!(
        group_target,
        ("name", ["rename", "changename", "setname", "rn"])
    );
    let group_name_cmd = [
        command!(group_name => "group_show_name").help("Shows the group's name"),
        command!(group_name, ("clear", ["c"]) => "group_clear_name")
            .flag(("yes", ["y"]))
            .help("Clears the group's name"),
        command!(group_name, ("name", OpaqueString) => "group_rename").help("Renames the group"),
    ]
    .into_iter();

    let group_display_name = tokens!(group_target, ("displayname", ["dn", "nick", "nickname"]));
    let group_display_name_cmd = [
        command!(group_display_name => "group_show_display_name")
            .help("Shows the group's display name"),
        command!(group_display_name, ("clear", ["c"]) => "group_clear_display_name")
            .flag(("yes", ["y"]))
            .help("Clears the group's display name"),
        command!(group_display_name, ("name", OpaqueString) => "group_change_display_name")
            .help("Changes the group's display name"),
    ]
    .into_iter();

    let group_description = tokens!(
        group_target,
        (
            "description",
            ["desc", "describe", "d", "bio", "info", "text", "intro"]
        )
    );
    let group_description_cmd = [
        command!(group_description => "group_show_description")
            .help("Shows the group's description"),
        command!(group_description, ("clear", ["c"]) => "group_clear_description")
            .flag(("yes", ["y"]))
            .help("Clears the group's description"),
        command!(group_description, ("description", OpaqueString) => "group_change_description")
            .help("Changes the group's description"),
    ]
    .into_iter();

    let group_icon = tokens!(
        group_target,
        ("icon", ["avatar", "picture", "image", "pic", "pfp"])
    );
    let group_icon_cmd = [
        command!(group_icon => "group_show_icon").help("Shows the group's icon"),
        command!(group_icon, ("clear", ["c"]) => "group_clear_icon")
            .flag(("yes", ["y"]))
            .help("Clears the group's icon"),
        command!(group_icon, ("icon", Avatar) => "group_change_icon")
            .help("Changes the group's icon"),
    ]
    .into_iter();

    let group_banner = tokens!(group_target, ("banner", ["splash", "cover"]));
    let group_banner_cmd = [
        command!(group_banner => "group_show_banner").help("Shows the group's banner"),
        command!(group_banner, ("clear", ["c"]) => "group_clear_banner")
            .flag(("yes", ["y"]))
            .help("Clears the group's banner"),
        command!(group_banner, ("banner", Avatar) => "group_change_banner")
            .help("Changes the group's banner"),
    ]
    .into_iter();

    let group_color = tokens!(group_target, ("color", ["colour"]));
    let group_color_cmd = [
        command!(group_color => "group_show_color").help("Shows the group's color"),
        command!(group_color, ("clear", ["c"]) => "group_clear_color")
            .flag(("yes", ["y"]))
            .help("Clears the group's color"),
        command!(group_color, ("color", OpaqueString) => "group_change_color")
            .help("Changes the group's color"),
    ]
    .into_iter();

    let group_privacy = tokens!(group_target, ("privacy", ["priv"]));
    let group_privacy_cmd = [
            command!(group_privacy => "group_show_privacy")
                .help("Shows the group's privacy settings"),
            command!(group_privacy, ("all", ["a"]), ("level", PrivacyLevel) => "group_change_privacy_all")
                .help("Changes all privacy settings for the group"),
            command!(group_privacy, ("privacy", GroupPrivacyTarget), ("level", PrivacyLevel) => "group_change_privacy")
                .help("Changes a specific privacy setting for the group"),
        ]
        .into_iter();

    let group_public_cmd = [
        command!(group_target, ("public", ["pub"]) => "group_set_public")
            .help("Sets the group to public"),
    ]
    .into_iter();

    let group_private_cmd = [
        command!(group_target, ("private", ["priv"]) => "group_set_private")
            .help("Sets the group to private"),
    ]
    .into_iter();

    let group_delete_cmd = [
        command!(group_target, ("delete", ["destroy", "erase", "yeet"]) => "group_delete")
            .flag(("yes", ["y"]))
            .help("Deletes the group"),
    ]
    .into_iter();

    let group_id_cmd =
        [command!(group_target, "id" => "group_id").help("Shows the group's ID")].into_iter();

    let group_front = tokens!(group_target, ("front", ["fronter", "fronters", "f"]));
    let group_front_cmd = [
        command!(group_front, ("percent", ["p", "%"]) => "group_fronter_percent")
            .flag(("duration", OpaqueString))
            .flag(("fronters-only", ["fo"]))
            .flag("flat"),
    ]
    .into_iter();

    let apply_list_opts = |cmd: Command| cmd.flags(get_list_flags());

    let group_list_members = tokens!(group_target, ("members", ["list", "ls"]));
    let group_list_members_cmd = [
        command!(group_list_members => "group_list_members"),
        command!(group_list_members, "list" => "group_list_members"),
        command!(group_list_members, ("search", ["find", "query"]), ("query", OpaqueStringRemainder) => "group_search_members"),
    ]
    .into_iter()
    .map(apply_list_opts);

    let group_modify_members_cmd = [
        command!(group_target, "add", Optional(MemberRefs) => "group_add_member")
            .flag(("all", ["a"])),
        command!(group_target, ("remove", ["delete", "del", "rem"]), Optional(MemberRefs) => "group_remove_member")
            .flag(("all", ["a"])),
    ]
    .into_iter();

    let system_groups_cmd = [
        command!(group, ("list", ["ls"]) => "group_list_groups"),
        command!(group, ("search", ["find", "query"]), ("query", OpaqueStringRemainder) => "group_search_groups"),
    ]
    .into_iter()
    .map(apply_list_opts);

    group_new_cmd
        .chain(group_info_cmd)
        .chain(group_name_cmd)
        .chain(group_display_name_cmd)
        .chain(group_description_cmd)
        .chain(group_icon_cmd)
        .chain(group_banner_cmd)
        .chain(group_color_cmd)
        .chain(group_privacy_cmd)
        .chain(group_public_cmd)
        .chain(group_private_cmd)
        .chain(group_front_cmd)
        .chain(group_modify_members_cmd)
        .chain(group_delete_cmd)
        .chain(group_id_cmd)
        .chain(group_list_members_cmd)
        .chain(system_groups_cmd)
}
