use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let switch = ("switch", ["sw"]);

    let edit = ("edit", ["e", "replace"]);
    let r#move = ("move", ["m", "shift", "offset"]);
    let delete = ("delete", ["remove", "erase", "cancel", "yeet"]);
    let copy = ("copy", ["add", "duplicate", "dupe"]);
    let out = "out";

    [
        command!(switch, out => "switch_out"),
        command!(switch, r#move, OpaqueString => "switch_move"), // TODO: datetime parsing
        command!(switch, delete => "switch_delete").flag(("all", ["clear", "c"])),
        command!(switch, edit, out => "switch_edit_out"),
        command!(switch, edit, MemberRefs => "switch_edit")
            .flag(("first", ["f"]))
            .flag(("remove", ["r"]))
            .flag(("append", ["a"]))
            .flag(("prepend", ["p"])),
        command!(switch, copy, MemberRefs => "switch_copy")
            .flag(("first", ["f"]))
            .flag(("remove", ["r"]))
            .flag(("append", ["a"]))
            .flag(("prepend", ["p"])),
        command!(switch, ("commands", ["help"]) => "switch_commands"),
        command!(switch, MemberRefs => "switch_do"),
    ]
    .into_iter()
}
