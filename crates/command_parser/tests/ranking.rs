use command_parser::{Tree, command::Command, parameter::*, parse_command, tokens};

#[test]
fn test_typoed_command_with_parameter() {
    let message_token = ("message", ["msg", "messageinfo"]);
    let author_token = ("author", ["sender", "a"]);

    // message <optional msg ref> author
    let cmd = Command::new(
        tokens!(message_token, Optional(MESSAGE_REF), author_token),
        "message_author",
    )
    .help("Shows the author of a proxied message");

    let mut tree = Tree::default();
    tree.register_command(cmd);

    let input = "message 1 auth";
    let result = parse_command(tree, "pk;".to_string(), input.to_string());

    match result {
        Ok(_) => panic!("Should have failed to parse"),
        Err(msg) => {
            println!("Error: {}", msg);
            assert!(msg.contains("Perhaps you meant one of the following commands"));
            assert!(msg.contains("message <target message link/id> author"));
        }
    }
}

#[test]
fn test_typoed_command_with_flags() {
    let message_token = ("message", ["msg", "messageinfo"]);
    let author_token = ("author", ["sender", "a"]);

    let cmd = Command::new(tokens!(message_token, author_token), "message_author")
        .flag(("flag", ["f"]))
        .flag(("flag2", ["f2"]))
        .help("Shows the author of a proxied message");

    let mut tree = Tree::default();
    tree.register_command(cmd);

    let input = "message auth -f -flag2";
    let result = parse_command(tree, "pk;".to_string(), input.to_string());

    match result {
        Ok(_) => panic!("Should have failed to parse"),
        Err(msg) => {
            println!("Error: {}", msg);
            assert!(msg.contains("Perhaps you meant one of the following commands"));
            assert!(msg.contains("message author"));
        }
    }
}
