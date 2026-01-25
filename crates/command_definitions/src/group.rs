use std::iter::once;

use command_parser::token::TokensIterator;

use crate::utils::get_list_flags;

use super::*;

pub fn group() -> (&'static str, [&'static str; 1]) {
    ("group", ["g"])
}

pub fn targeted() -> TokensIterator {
    tokens!(group(), GroupRef)
}

pub fn cmds() -> impl Iterator<Item = Command> {
    let group = group();
    let group_target = targeted();

    let group_new = tokens!(group, ("new", ["n"]));
    let group_new_cmd = once(
        command!(group_new, Remainder(("name", OpaqueString)) => "group_new")
            .flag(YES)
            .help("Creates a new group"),
    );

    let group_info_cmd = once(
        command!(group_target => "group_info")
            .flag(ALL)
            .help("Looks up information about a group"),
    );

    let group_name = tokens!(
        group_target,
        ("name", ["rename", "changename", "setname", "rn"])
    );
    let group_name_cmd = [
        command!(group_name => "group_show_name").help("Shows the group's name"),
        command!(group_name, CLEAR => "group_clear_name")
            .help("Clears the group's name"),
        command!(group_name, Remainder(("name", OpaqueString)) => "group_rename")
            .flag(YES)
            .help("Renames a group"),
    ];

    let group_display_name = tokens!(group_target, ("displayname", ["dn", "nick", "nickname"]));
    let group_display_name_cmd = [
        command!(group_display_name => "group_show_display_name")
            .help("Shows the group's display name"),
        command!(group_display_name, CLEAR => "group_clear_display_name")
            .help("Clears the group's display name"),
        command!(group_display_name, Remainder(("name", OpaqueString)) => "group_change_display_name")
            .help("Changes the group's display name"),
    ];

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
        command!(group_description, CLEAR => "group_clear_description")
            .help("Clears the group's description"),
        command!(group_description, Remainder(("description", OpaqueString)) => "group_change_description")
            .help("Changes the group's description"),
    ];

    let group_icon = tokens!(
        group_target,
        ("icon", ["avatar", "picture", "image", "pic", "pfp"])
    );
    let group_icon_cmd = [
        command!(group_icon => "group_show_icon").help("Shows the group's icon"),
        command!(group_icon, CLEAR => "group_clear_icon")
            .flag(YES)
            .help("Clears the group's icon"),
        command!(group_icon, ("icon", Avatar) => "group_change_icon")
            .help("Changes a group's icon"),
    ];

    let group_banner = tokens!(group_target, ("banner", ["splash", "cover"]));
    let group_banner_cmd = [
        command!(group_banner => "group_show_banner").help("Sets the group's banner image"),
        command!(group_banner, CLEAR => "group_clear_banner")
            .flag(YES)
            .help("Clears the group's banner"),
        command!(group_banner, ("banner", Avatar) => "group_change_banner")
            .help("Sets the group's banner image"),
    ];

    let group_color = tokens!(group_target, ("color", ["colour"]));
    let group_color_cmd = [
        command!(group_color => "group_show_color").help("Shows the group's color"),
        command!(group_color, CLEAR => "group_clear_color")
            .help("Clears the group's color"),
        command!(group_color, ("color", OpaqueString) => "group_change_color")
            .help("Changes a group's color"),
    ];

    let group_privacy = tokens!(group_target, ("privacy", ["priv"]));
    let group_privacy_cmd = [
            command!(group_privacy => "group_show_privacy")
                .help("Shows the group's privacy settings"),
            command!(group_privacy, ALL, ("level", PrivacyLevel) => "group_change_privacy_all")
                .help("Changes all privacy settings for the group"),
            command!(group_privacy, ("privacy", GroupPrivacyTarget), ("level", PrivacyLevel) => "group_change_privacy")
                .help("Changes a specific privacy setting for the group"),
    ];

    let group_public_cmd = [
        command!(group_target, ("public", ["pub"]) => "group_set_public")
            .help("Sets the group to public"),
    ];

    let group_private_cmd = [
        command!(group_target, ("private", ["priv"]) => "group_set_private")
            .help("Sets the group to private"),
    ];

    let group_delete_cmd = [
        command!(group_target, ("delete", ["destroy", "erase", "yeet"]) => "group_delete")
            .help("Deletes a group"),
    ];

    let group_id_cmd = [command!(group_target, "id" => "group_id").help("Prints a group's ID")];

    let group_front = tokens!(group_target, ("front", ["fronter", "fronters", "f"]));
    let group_front_cmd = [
        command!(group_front, ("percent", ["p", "%"]) => "group_fronter_percent")
            .help("Shows a group's front breakdown")
            .flag(("duration", OpaqueString))
            .flag(("fronters-only", ["fo"]))
            .flag("flat"),
    ];

    let apply_list_opts = |cmd: Command| cmd.flags(get_list_flags());
    let search_param = Optional(Remainder(("query", OpaqueString)));

    let group_list_members_cmd =
        once(command!(group_target, ("members", ["list", "ls"]), search_param => "group_members"))
            .map(apply_list_opts);

    let group_modify_members_cmd = [
        command!(group_target, "add", Optional(MemberRefs) => "group_add_member")
            .help("Adds one or more members to a group")
            .flag(ALL),
        command!(group_target, ("remove", ["rem", "rm"]), Optional(MemberRefs) => "group_remove_member")
            .help("Removes one or more members from a group")
            .flag(ALL).flag(YES),
    ];

    let system_groups_cmd =
        once(command!(group, ("list", ["ls", "l"]), search_param => "groups_self"))
            .map(apply_list_opts);

    system_groups_cmd
        .chain(group_new_cmd)
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
}
