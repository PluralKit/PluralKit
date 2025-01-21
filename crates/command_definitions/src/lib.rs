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

use command_parser::{
    command, command::Command, concat_tokens, parameter::ParameterKind::*, tokens,
};

pub fn all() -> impl Iterator<Item = Command> {
    (help::cmds())
        .chain(system::cmds())
        .chain(member::cmds())
        .chain(config::cmds())
        .chain(fun::cmds())
}
