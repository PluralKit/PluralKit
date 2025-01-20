#![feature(iter_intersperse)]

use commands::commands as cmds;

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
        for command in cmds::all() {
            println!("{} - {}", command, command.help);
        }
    }
}
