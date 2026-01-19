use command_parser::{parse_command, Tree, command::Command, parameter::*, tokens};

/// this checks if we properly keep track of filtered tokens (eg. branches we failed on)
/// when we backtrack. a previous parser bug would cause infinite loops since it did not
/// (the parser would "flip-flop" between branches) this is here for reference.
#[test]
fn test_infinite_loop_repro() {
    let p1 = Optional(("param1", ParameterKind::OpaqueString));
    let p2 = Optional(("param2", ParameterKind::OpaqueString));
    
    let cmd1 = Command::new(tokens!("s", p1, "A"), "cmd1");
    let cmd2 = Command::new(tokens!("s", p2, "B"), "cmd2");
    
    let mut tree = Tree::default();
    tree.register_command(cmd1);
    tree.register_command(cmd2);
    
    let input = "s foo C";
    // this should fail and not loop
    let result = parse_command(tree, "pk;".to_string(), input.to_string());
    assert!(result.is_err());
}

/// check if we have params from other branches when we trying to match them and they succeeded
/// but then we backtracked, making them invalid. this should no longer happen since we just
/// extract params from matched tokens when we match the command, but keeping here just for reference.
#[test]
fn test_dirty_params() {
    let p1 = Optional(("param1", ParameterKind::OpaqueString));
    let p2 = Optional(("param2", ParameterKind::OpaqueString));
    
    let cmd1 = Command::new(tokens!("s", p1, "A"), "cmd1");
    let cmd2 = Command::new(tokens!("s", p2, "B"), "cmd2");
    
    let mut tree = Tree::default();
    tree.register_command(cmd1);
    tree.register_command(cmd2);
    
    let input = "s foo B";
    let result = parse_command(tree, "pk;".to_string(), input.to_string()).unwrap();
    
    println!("params: {:?}", result.parameters);
    assert!(!result.parameters.contains_key("param1"), "params should not contain 'param1' from failed branch");
    assert!(result.parameters.contains_key("param2"), "params should contain 'param2'");
}
