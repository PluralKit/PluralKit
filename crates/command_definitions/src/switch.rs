use super::*;

pub fn cmds() -> impl IntoIterator<Item = Command> {
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
        command!(switch, ("commands", ["help"]) => "switch_commands")
            .help("Shows help for switch commands"),
        command!(switch, out => "switch_out").help("Registers a switch with no members"),
        command!(switch, delete => "switch_delete")
            .help("Deletes the latest switch")
            .flag(("all", ["clear", "c"])),
        command!(switch, r#move, Remainder(OpaqueString) => "switch_move")
            .help("Moves the latest switch in time"), // TODO: datetime parsing
        command!(switch, edit, out => "switch_edit_out")
            .help("Turns the latest switch into a switch-out")
            .flag(YES),
        command!(switch, edit, Optional(MemberRefs) => "switch_edit")
            .help("Edits the members in the latest switch")
            .flags(edit_flags),
        command!(switch, copy, Optional(MemberRefs) => "switch_copy")
            .help("Makes a new switch with the listed members added")
            .flags(edit_flags),
        command!(switch, MemberRefs => "switch_do").help("Registers a switch"),
    ]
}
