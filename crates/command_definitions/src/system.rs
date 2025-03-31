use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let system = ("system", ["s"]);
    let system_target = tokens!(system, SystemRef);

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
        command!(system_name, ("clear", ["c"]) => "system_clear_name")
            .help("Clears the system's name"),
        command!(system_name, ("name", OpaqueString) => "system_rename")
            .help("Renames a given system"),
    ]
    .into_iter();

    let system_server_name = tokens!(system_target, ("servername", ["sn", "guildname"]));
    let system_server_name_cmd = [
        command!(system_server_name => "system_show_server_name")
            .help("Shows the system's server name"),
        command!(system_server_name, ("clear", ["c"]) => "system_clear_server_name")
            .help("Clears the system's server name"),
        command!(system_server_name, ("name", OpaqueString) => "system_rename_server_name")
            .help("Renames the system's server name"),
    ]
    .into_iter();

    let system_new = tokens!(system, ("new", ["n"]));
    let system_new_cmd = [
        command!(system_new => "system_new").help("Creates a new system"),
        command!(system_new, ("name", OpaqueString) => "system_new_name")
            .help("Creates a new system (using the provided name)"),
    ]
    .into_iter();

    system_info_cmd
        .chain(system_name_cmd)
        .chain(system_server_name_cmd)
        .chain(system_new_cmd)
}
