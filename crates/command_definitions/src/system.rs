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
    let system_name_cmd = [
        command!(system_name => "system_show_name").help("Shows the systems name"),
    ]
    .into_iter();

    let system_name_self = tokens!(system, "name");
    let system_name_self_cmd = [
        command!(system_name_self => "system_show_name_self").help("Shows your system's name"),
        command!(system_name_self, ("clear", ["c"]) => "system_clear_name")
            .help("Clears your system's name"),
        command!(system_name_self, ("name", OpaqueString) => "system_rename")
            .help("Renames your system"),
    ]
    .into_iter();

    let system_server_name = tokens!(system_target, ("servername", ["sn", "guildname"]));
    let system_server_name_cmd = [
        command!(system_server_name => "system_show_server_name")
            .help("Shows the system's server name"),
    ]
    .into_iter();

    let system_server_name_self = tokens!(system, ("servername", ["sn", "guildname"]));
    let system_server_name_self_cmd = [
        command!(system_server_name_self => "system_show_server_name_self")
            .help("Shows your system's server name"),
        command!(system_server_name_self, ("clear", ["c"]) => "system_clear_server_name")
            .help("Clears your system's server name"),
        command!(system_server_name_self, ("name", OpaqueString) => "system_rename_server_name")
            .help("Renames your system's server name"),
    ]
    .into_iter();

    let system_description = tokens!(system_target, ("description", ["desc", "d"]));
    let system_description_cmd = [
        command!(system_description => "system_show_description").help("Shows the system's description"),
    ]
    .into_iter();

    let system_description_self = tokens!(system, ("description", ["desc", "d"]));
    let system_description_self_cmd = [
        command!(system_description_self => "system_show_description_self").help("Shows your system's description"),
        command!(system_description_self, ("clear", ["c"]) => "system_clear_description")
            .help("Clears your system's description"),
        command!(system_description_self, ("description", OpaqueString) => "system_change_description")
            .help("Changes your system's description"),
    ]
    .into_iter();

    let system_color = tokens!(system_target, ("color", ["colour"]));
    let system_color_cmd = [
        command!(system_color => "system_show_color").help("Shows the system's color"),
    ]
    .into_iter();

    let system_color_self = tokens!(system, ("color", ["colour"]));
    let system_color_self_cmd = [
        command!(system_color_self => "system_show_color_self").help("Shows your system's color"),
        command!(system_color_self, ("clear", ["c"]) => "system_clear_color")
            .help("Clears your system's color"),
        command!(system_color_self, ("color", OpaqueString) => "system_change_color")
            .help("Changes your system's color"),
    ]
    .into_iter();

    let system_tag = tokens!(system_target, ("tag", ["suffix"]));
    let system_tag_cmd = [
        command!(system_tag => "system_show_tag").help("Shows the system's tag"),
    ]
    .into_iter();

    let system_tag_self = tokens!(system, ("tag", ["suffix"]));
    let system_tag_self_cmd = [
        command!(system_tag_self => "system_show_tag_self").help("Shows your system's tag"),
        command!(system_tag_self, ("clear", ["c"]) => "system_clear_tag")
            .help("Clears your system's tag"),
        command!(system_tag_self, ("tag", OpaqueString) => "system_change_tag")
            .help("Changes your system's tag"),
    ]
    .into_iter();

    let system_server_tag = tokens!(system_target, ("servertag", ["st", "guildtag"]));
    let system_server_tag_cmd = [
        command!(system_server_tag => "system_show_server_tag").help("Shows the system's server tag"),
    ]
    .into_iter();

    let system_server_tag_self = tokens!(system, ("servertag", ["st", "guildtag"]));
    let system_server_tag_self_cmd = [
        command!(system_server_tag_self => "system_show_server_tag_self").help("Shows your system's server tag"),
        command!(system_server_tag_self, ("clear", ["c"]) => "system_clear_server_tag")
            .help("Clears your system's server tag"),
        command!(system_server_tag_self, ("tag", OpaqueString) => "system_change_server_tag")
            .help("Changes your system's server tag"),
    ]
    .into_iter();

    let system_pronouns = tokens!(system_target, ("pronouns", ["prns"]));
    let system_pronouns_cmd = [
        command!(system_pronouns => "system_show_pronouns").help("Shows the system's pronouns"),
    ]
    .into_iter();

    let system_pronouns_self = tokens!(system, ("pronouns", ["prns"]));
    let system_pronouns_self_cmd = [
        command!(system_pronouns_self => "system_show_pronouns_self").help("Shows your system's pronouns"),
        command!(system_pronouns_self, ("clear", ["c"]) => "system_clear_pronouns")
            .flag(("yes", ["y"]))
            .help("Clears your system's pronouns"),
        command!(system_pronouns_self, ("pronouns", OpaqueString) => "system_change_pronouns")
            .help("Changes your system's pronouns"),
    ]
    .into_iter();

    system_new_cmd
        .chain(system_name_self_cmd)
        .chain(system_server_name_self_cmd)
        .chain(system_description_self_cmd)
        .chain(system_color_self_cmd)
        .chain(system_tag_self_cmd)
        .chain(system_server_tag_self_cmd)
        .chain(system_pronouns_self_cmd)
        .chain(system_name_cmd)
        .chain(system_server_name_cmd)
        .chain(system_description_cmd)
        .chain(system_color_cmd)
        .chain(system_tag_cmd)
        .chain(system_server_tag_cmd)
        .chain(system_pronouns_cmd)
        .chain(system_info_cmd)
}