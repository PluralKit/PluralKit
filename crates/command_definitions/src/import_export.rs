use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    [
        command!("import", Optional(("url", OpaqueStringRemainder)) => "import")
            .flag(("yes", ["y"])),
        command!("export" => "export"),
    ]
    .into_iter()
}
