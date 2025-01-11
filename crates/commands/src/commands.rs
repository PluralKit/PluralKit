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

use std::fmt::{Debug, Display};

use smol_str::SmolStr;

use crate::{
    command,
    token::{ToToken, Token},
};

#[derive(Debug, Clone)]
pub struct Command {
    // TODO: fix hygiene
    pub tokens: Vec<Token>,
    pub help: SmolStr,
    pub cb: SmolStr,
    pub show_in_suggestions: bool,
}

impl Command {
    pub fn new(
        tokens: impl IntoIterator<Item = Token>,
        help: impl Into<SmolStr>,
        cb: impl Into<SmolStr>,
        show_in_suggestions: bool,
    ) -> Self {
        Self {
            tokens: tokens.into_iter().collect(),
            help: help.into(),
            cb: cb.into(),
            show_in_suggestions,
        }
    }
}

impl Display for Command {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        for (idx, token) in self.tokens.iter().enumerate() {
            write!(f, "{}", token)?;
            if idx < self.tokens.len() - 1 {
                write!(f, " ")?;
            }
        }
        Ok(())
    }
}

#[macro_export]
macro_rules! command {
    ([$($v:expr),+], $cb:expr, $help:expr, suggest = $suggest:expr) => {
        $crate::commands::Command::new([$($v.to_token()),*], $help, $cb, $suggest)
    };
    ([$($v:expr),+], $cb:expr, $help:expr) => {
        $crate::command!([$($v),+], $cb, $help, suggest = true)
    };
}

pub fn all() -> Vec<Command> {
    (help::cmds())
        .chain(system::cmds())
        .chain(member::cmds())
        .chain(config::cmds())
        .chain(fun::cmds())
        .collect()
}
