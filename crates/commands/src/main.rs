#![feature(iter_intersperse)]

use command_parser::{token::Token, Tree};
use commands::COMMAND_TREE;

fn main() {
    let cmd = std::env::args()
        .skip(1)
        .intersperse(" ".to_string())
        .collect::<String>();
    if !cmd.is_empty() {
        use commands::CommandResult;
        let parsed = commands::parse_command("pk;".to_string(), cmd);
        match parsed {
            CommandResult::Ok { command } => println!("{command:#?}"),
            CommandResult::Err { error } => println!("{error}"),
        }
    } else {
        for command in command_definitions::all() {
            println!("{} - {}", command, command.help);
        }
    }
}

fn print_tree(tree: &Tree, depth: usize) {
    println!();
    for (token, branch) in tree.branches() {
        for _ in 0..depth {
            print!(" ");
        }
        for _ in 0..depth {
            print!("-");
        }
        print!("> {token:?}");
        if matches!(token, Token::Empty) {
            println!(": {}", branch.command().unwrap().cb)
        } else {
            print_tree(branch, depth + 1)
        }
    }
}
