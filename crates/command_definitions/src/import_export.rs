use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    [
        command!("import", Optional(Remainder(("url", OpaqueString))) => "import").flag(YES),
        command!("export" => "export"),
    ]
    .into_iter()
}
