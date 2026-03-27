use command_parser::{
    parameter::{MESSAGE_LINK, MESSAGE_REF},
    token::TokensIterator,
};

use super::*;

pub fn cmds() -> impl IntoIterator<Item = Command> {
    let message = tokens!(("message", ["msg", "messageinfo"]), Optional(MESSAGE_REF));

    let author = ("author", ["sender", "a"]);
    let delete = ("delete", ["del", "d"]);
    let reproxy = ("reproxy", ["rp", "crimes", "crime"]);

    let edit = ("edit", ["e"]);
    let new_content_param = Optional(Remainder(("new_content", OpaqueString)));
    let edit_short_subcmd = tokens!(Optional(MESSAGE_LINK), new_content_param);

    let apply_edit = |cmd: Command| {
        cmd.flag(("append", ["a"]))
            .flag(("prepend", ["p"]))
            .flag(("regex", ["r"]))
            .flag(("no-space", ["nospace", "ns"]))
            .flag(("clear-embeds", ["clear-embed", "ce"]))
            .flag(("clear-attachments", ["clear-attachment", "ca"]))
            .help("Edits a previously proxied message")
    };
    let make_edit_cmd = |tokens: TokensIterator| apply_edit(command!(tokens => "message_edit"));

    [
        make_edit_cmd(tokens!(edit, edit_short_subcmd)),
        // this one always does regex
        make_edit_cmd(tokens!("x", edit_short_subcmd)).flag_value("regex", None),
        command!(reproxy, Optional(("msg", MESSAGE_REF)), ("member", MemberRef) => "message_reproxy")
            .help("Reproxies a previously proxied message with a different member"),
        command!(message, author => "message_author").help("Shows the author of a proxied message"),
        command!(message, delete => "message_delete").help("Deletes a proxied message"),
        make_edit_cmd(tokens!(message, edit, new_content_param)),
        command!(message => "message_info")
            .flag(delete)
            .flag(author)
            .help("Shows information about a proxied message"),
    ]
}
