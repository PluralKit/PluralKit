use commands::commands as cmds;

fn main() {
    for command in cmds::all() {
        println!("{}", command);
    }
}
