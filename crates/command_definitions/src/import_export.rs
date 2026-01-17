use super::*;

pub fn cmds() -> impl IntoIterator<Item = Command> {
    [
        command!("import", Optional(Remainder(("url", OpaqueString))) => "import").flag(YES),
        command!("export" => "export"),
    ]
}
