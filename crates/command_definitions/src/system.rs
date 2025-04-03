use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    edit()
}

pub fn edit() -> impl Iterator<Item = Command> {
    let system = ("system", ["s"]);
    let system_target = tokens!(system, SystemRef);

    let system_new = tokens!(system, ("new", ["n"]));
    let system_new_cmd = [
        command!(system_new => "system_new").help("Creates a new system"),
        command!(system_new, ("name", OpaqueString) => "system_new_name")
            .help("Creates a new system (using the provided name)"),
    ]
    .into_iter();

    let system_info_cmd = [
        command!(system => "system_info_self").help("Shows information about your system"),
        command!(system_target, ("info", ["show", "view"]) => "system_info")
            .help("Shows information about your system"),
    ]
    .into_iter()
    .map(|cmd| {
        cmd.flag(("public", ["pub"]))
            .flag(("private", ["priv"]))
            .flag(("all", ["a"]))
    });

    let system_name = tokens!(system_target, "name");
    let system_name_cmd =
        [command!(system_name => "system_show_name").help("Shows the systems name")].into_iter();

    let system_name_self = tokens!(system, "name");
    let system_name_self_cmd = [
        command!(system_name_self => "system_show_name_self").help("Shows your system's name"),
        command!(system_name_self, ("clear", ["c"]) => "system_clear_name")
            .flag(("yes", ["y"]))
            .help("Clears your system's name"),
        command!(system_name_self, ("name", OpaqueString) => "system_rename")
            .help("Renames your system"),
    ]
    .into_iter();

    let system_server_name = tokens!(system_target, ("servername", ["sn", "guildname"]));
    let system_server_name_cmd = [command!(system_server_name => "system_show_server_name")
        .help("Shows the system's server name")]
    .into_iter();

    let system_server_name_self = tokens!(system, ("servername", ["sn", "guildname"]));
    let system_server_name_self_cmd = [
        command!(system_server_name_self => "system_show_server_name_self")
            .help("Shows your system's server name"),
        command!(system_server_name_self, ("clear", ["c"]) => "system_clear_server_name")
            .flag(("yes", ["y"]))
            .help("Clears your system's server name"),
        command!(system_server_name_self, ("name", OpaqueString) => "system_rename_server_name")
            .help("Renames your system's server name"),
    ]
    .into_iter();

    let system_description = tokens!(system_target, ("description", ["desc", "d"]));
    let system_description_cmd = [command!(system_description => "system_show_description")
        .help("Shows the system's description")]
    .into_iter();

    let system_description_self = tokens!(system, ("description", ["desc", "d"]));
    let system_description_self_cmd = [
        command!(system_description_self => "system_show_description_self").help("Shows your system's description"),
        command!(system_description_self, ("clear", ["c"]) => "system_clear_description")
        .flag(("yes", ["y"]))
            .help("Clears your system's description"),
        command!(system_description_self, ("description", OpaqueString) => "system_change_description")
            .help("Changes your system's description"),
    ]
    .into_iter();

    let system_color = tokens!(system_target, ("color", ["colour"]));
    let system_color_cmd =
        [command!(system_color => "system_show_color").help("Shows the system's color")]
            .into_iter();

    let system_color_self = tokens!(system, ("color", ["colour"]));
    let system_color_self_cmd = [
        command!(system_color_self => "system_show_color_self").help("Shows your system's color"),
        command!(system_color_self, ("clear", ["c"]) => "system_clear_color")
            .flag(("yes", ["y"]))
            .help("Clears your system's color"),
        command!(system_color_self, ("color", OpaqueString) => "system_change_color")
            .help("Changes your system's color"),
    ]
    .into_iter();

    let system_tag = tokens!(system_target, ("tag", ["suffix"]));
    let system_tag_cmd =
        [command!(system_tag => "system_show_tag").help("Shows the system's tag")].into_iter();

    let system_tag_self = tokens!(system, ("tag", ["suffix"]));
    let system_tag_self_cmd = [
        command!(system_tag_self => "system_show_tag_self").help("Shows your system's tag"),
        command!(system_tag_self, ("clear", ["c"]) => "system_clear_tag")
            .flag(("yes", ["y"]))
            .help("Clears your system's tag"),
        command!(system_tag_self, ("tag", OpaqueString) => "system_change_tag")
            .help("Changes your system's tag"),
    ]
    .into_iter();

    let system_server_tag = tokens!(system_target, ("servertag", ["st", "guildtag"]));
    let system_server_tag_cmd = [command!(system_server_tag => "system_show_server_tag")
        .help("Shows the system's server tag")]
    .into_iter();

    let system_server_tag_self = tokens!(system, ("servertag", ["st", "guildtag"]));
    let system_server_tag_self_cmd = [
        command!(system_server_tag_self => "system_show_server_tag_self")
            .help("Shows your system's server tag"),
        command!(system_server_tag_self, ("clear", ["c"]) => "system_clear_server_tag")
            .flag(("yes", ["y"]))
            .help("Clears your system's server tag"),
        command!(system_server_tag_self, ("tag", OpaqueString) => "system_change_server_tag")
            .help("Changes your system's server tag"),
    ]
    .into_iter();

    let system_pronouns = tokens!(system_target, ("pronouns", ["prns"]));
    let system_pronouns_cmd =
        [command!(system_pronouns => "system_show_pronouns").help("Shows the system's pronouns")]
            .into_iter();

    let system_pronouns_self = tokens!(system, ("pronouns", ["prns"]));
    let system_pronouns_self_cmd = [
        command!(system_pronouns_self => "system_show_pronouns_self")
            .help("Shows your system's pronouns"),
        command!(system_pronouns_self, ("clear", ["c"]) => "system_clear_pronouns")
            .flag(("yes", ["y"]))
            .help("Clears your system's pronouns"),
        command!(system_pronouns_self, ("pronouns", OpaqueString) => "system_change_pronouns")
            .help("Changes your system's pronouns"),
    ]
    .into_iter();

    let system_avatar = tokens!(system_target, ("avatar", ["pfp"]));
    let system_avatar_cmd =
        [command!(system_avatar => "system_show_avatar").help("Shows the system's avatar")]
            .into_iter();

    let system_avatar_self = tokens!(system, ("avatar", ["pfp"]));
    let system_avatar_self_cmd = [
        command!(system_avatar_self => "system_show_avatar_self")
            .help("Shows your system's avatar"),
        command!(system_avatar_self, ("clear", ["c"]) => "system_clear_avatar")
            .flag(("yes", ["y"]))
            .help("Clears your system's avatar"),
        command!(system_avatar_self, ("avatar", Avatar) => "system_change_avatar")
            .help("Changes your system's avatar"),
    ]
    .into_iter();

    let system_server_avatar = tokens!(system_target, ("serveravatar", ["spfp"]));
    let system_server_avatar_cmd = [
        command!(system_server_avatar => "system_show_server_avatar")
            .help("Shows the system's server avatar"),
    ]
    .into_iter();

    let system_server_avatar_self = tokens!(system, ("serveravatar", ["spfp"]));
    let system_server_avatar_self_cmd = [
        command!(system_server_avatar_self => "system_show_server_avatar_self")
            .help("Shows your system's server avatar"),
        command!(system_server_avatar_self, ("clear", ["c"]) => "system_clear_server_avatar")
            .flag(("yes", ["y"]))
            .help("Clears your system's server avatar"),
        command!(system_server_avatar_self, ("avatar", Avatar) => "system_change_server_avatar")
            .help("Changes your system's server avatar"),
    ]
    .into_iter();

    let system_banner = tokens!(system_target, ("banner", ["cover"]));
    let system_banner_cmd =
        [command!(system_banner => "system_show_banner").help("Shows the system's banner")]
            .into_iter();

    let system_banner_self = tokens!(system, ("banner", ["cover"]));
    let system_banner_self_cmd = [
        command!(system_banner_self => "system_show_banner_self")
            .help("Shows your system's banner"),
        command!(system_banner_self, ("clear", ["c"]) => "system_clear_banner")
            .flag(("yes", ["y"]))
            .help("Clears your system's banner"),
        command!(system_banner_self, ("banner", Avatar) => "system_change_banner")
            .help("Changes your system's banner"),
    ]
    .into_iter();

    let system_delete = std::iter::once(
        command!(system, ("delete", ["erase", "remove", "yeet"]) => "system_delete")
            .flag(("no-export", ["ne"]))
            .help("Deletes the system"),
    );

    system_new_cmd
        .chain(system_name_self_cmd)
        .chain(system_server_name_self_cmd)
        .chain(system_description_self_cmd)
        .chain(system_color_self_cmd)
        .chain(system_tag_self_cmd)
        .chain(system_server_tag_self_cmd)
        .chain(system_pronouns_self_cmd)
        .chain(system_avatar_self_cmd)
        .chain(system_server_avatar_self_cmd)
        .chain(system_banner_self_cmd)
        .chain(system_delete)
        .chain(system_name_cmd)
        .chain(system_server_name_cmd)
        .chain(system_description_cmd)
        .chain(system_color_cmd)
        .chain(system_tag_cmd)
        .chain(system_server_tag_cmd)
        .chain(system_pronouns_cmd)
        .chain(system_avatar_cmd)
        .chain(system_server_avatar_cmd)
        .chain(system_banner_cmd)
        .chain(system_info_cmd)
}
