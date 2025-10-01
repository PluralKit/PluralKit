pub mod admin;
pub mod api;
pub mod autoproxy;
pub mod checks;
pub mod commands;
pub mod config;
pub mod dashboard;
pub mod debug;
pub mod fun;
pub mod group;
pub mod help;
pub mod import_export;
pub mod member;
pub mod message;
pub mod misc;
pub mod random;
pub mod server_config;
pub mod switch;
pub mod system;

pub mod utils;

use command_parser::{command, command::Command, parameter::ParameterKind::*, tokens};

pub fn all() -> impl Iterator<Item = Command> {
    (help::cmds())
        .chain(system::cmds())
        .chain(group::cmds())
        .chain(member::cmds())
        .chain(config::cmds())
        .chain(fun::cmds())
        .chain(switch::cmds())
        .chain(random::cmds())
        .chain(api::cmds())
        .map(|cmd| {
            cmd.hidden_flag(("plaintext", ["pt"]))
                .hidden_flag(("raw", ["r"]))
                .hidden_flag(("show-embed", ["se"]))
        })
}

pub const RESET: (&str, [&str; 2]) = ("reset", ["clear", "default"]);
