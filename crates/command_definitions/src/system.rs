use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let system = ["system", "s"];
    let new = ["new", "n"];

    let system_new = tokens!(system, new);

    [
        command!([system] => "system_show").help("Shows information about your system"),
        command!(system_new => "system_new").help("Creates a new system"),
        command!(system_new, ("name", OpaqueString) => "system_new_name")
            .help("Creates a new system (using the provided name)"),
    ]
    .into_iter()
}
