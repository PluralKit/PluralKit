pub mod admin;
pub mod api;
pub mod autoproxy;
pub mod config;
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

use command_parser::{
    command,
    command::Command,
    parameter::{Optional, Parameter, ParameterKind::*, Remainder, Skip},
    tokens,
};

pub fn all() -> impl Iterator<Item = Command> {
    std::iter::empty()
        .chain(help::cmds())
        .chain(system::cmds())
        .chain(group::cmds())
        .chain(member::cmds())
        .chain(config::cmds())
        .chain(server_config::cmds())
        .chain(fun::cmds())
        .chain(switch::cmds())
        .chain(random::cmds())
        .chain(api::cmds())
        .chain(autoproxy::cmds())
        .chain(debug::cmds())
        .chain(message::cmds())
        .chain(import_export::cmds())
        .chain(admin::cmds())
        .chain(misc::cmds())
        .map(|cmd| {
            cmd.hidden_flag(("plaintext", ["pt"]))
                .hidden_flag(("raw", ["r"]))
                .hidden_flag(("show-embed", ["se"]))
                .hidden_flag(("by-id", ["id"]))
                .hidden_flag(("private", ["priv"]))
                .hidden_flag(("public", ["pub"]))
        })
}

pub const RESET: (&str, [&str; 2]) = ("reset", ["clear", "default"]);

pub const CLEAR: (&str, [&str; 1]) = ("clear", ["c"]);
pub const YES: (&str, [&str; 1]) = ("yes", ["y"]);
pub const ALL: (&str, [&str; 1]) = ("all", ["a"]);
