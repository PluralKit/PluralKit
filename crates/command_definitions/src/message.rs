use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let message = tokens!(("message", ["msg", "messageinfo"]), MessageRef);

    let author = ("author", ["sender", "a"]);
    let delete = ("delete", ["del", "d"]);
    let reproxy = ("reproxy", ["rp", "crimes", "crime"]);

    let edit = ("edit", ["e"]);
    let new_content_param = Remainder(("new_content", OpaqueString));
    let apply_edit = |cmd: Command| {
        cmd.flag(("append", ["a"]))
            .flag(("prepend", ["p"]))
            .flag(("regex", ["r"]))
            .flag(("no-space", ["nospace", "ns"]))
            .flag(("clear-embeds", ["clear-embed", "ce"]))
            .flag(("clear-attachments", ["clear-attachment", "ca"]))
            .help("Edits a proxied message")
    };

    [
        command!(message => "message_info")
            .flag(delete)
            .flag(author)
            .help("Shows information about a proxied message"),
        command!(message, author => "message_author").help("Shows the author of a proxied message"),
        command!(message, delete => "message_delete").help("Deletes a proxied message"),
        apply_edit(command!(message, edit, new_content_param => "message_edit_specified")),
        apply_edit(command!(edit, Skip(MessageRef), new_content_param => "message_edit_specified")),
        apply_edit(command!(edit, new_content_param => "message_edit")),
        command!(reproxy, ("member", MemberRef) => "message_reproxy")
            .help("Reproxies a message with a different member"),
        command!(reproxy, ("msg", MessageRef), ("member", MemberRef) => "message_reproxy_specified")
            .help("Reproxies a message with a different member"),
    ]
    .into_iter()
}
