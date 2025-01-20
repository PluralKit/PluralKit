use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let system = ["system", "s"];
    let new = ["new", "n"];

    [
        command!([system], "system_show").help("Shows information about your system"),
        command!([system, new], "system_new").help("Creates a new system"),
        command!([system, new, ("name", OpaqueString::SINGLE)], "system_new")
            .help("Creates a new system"),
    ]
    .into_iter()
}
