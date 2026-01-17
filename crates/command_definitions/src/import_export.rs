use super::*;

pub fn cmds() -> impl IntoIterator<Item = Command> {
    [
        command!("import", Optional(Remainder(("url", OpaqueString))) => "import")
            .help("Imports system information from a data file")
            .flag(YES),
        command!("export" => "export").help("Exports system information to a file"),
    ]
}
