#![feature(iter_intersperse)]

use commands::commands as cmds;

fn main() {
    let cmd = std::env::args()
        .skip(1)
        .intersperse(" ".to_string())
        .collect::<String>();
    if !cmd.is_empty() {
        let parsed = commands::parse_command(cmd);
        println!("{:#?}", parsed);
    } else {
        for command in cmds::all() {
            println!("{}", command);
        }
    }
}
