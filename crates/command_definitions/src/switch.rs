use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let switch = ("switch", ["sw"]);

    let edit = ("edit", ["e", "replace"]);
    let r#move = ("move", ["m", "shift", "offset"]);
    let delete = ("delete", ["remove", "erase", "cancel", "yeet"]);
    let copy = ("copy", ["add", "duplicate", "dupe"]);
    let out = "out";

    let edit_flags = [
        ("first", ["f"]),
        ("remove", ["r"]),
        ("append", ["a"]),
        ("prepend", ["p"]),
    ];

    [
        command!(switch, out => "switch_out"),
        command!(switch, r#move, OpaqueString => "switch_move"), // TODO: datetime parsing
        command!(switch, delete => "switch_delete").flag(("all", ["clear", "c"])),
        command!(switch, edit, out => "switch_edit_out").flag(("yes", ["y"])),
        command!(switch, edit, Optional(MemberRefs) => "switch_edit").flags(edit_flags),
        command!(switch, copy, Optional(MemberRefs) => "switch_copy").flags(edit_flags),
        command!(switch, ("commands", ["help"]) => "switch_commands"),
        command!(switch, Optional(MemberRefs) => "switch_do"),
    ]
    .into_iter()
}
