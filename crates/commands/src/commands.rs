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

use crate::{any, command, flag::Flag, parameter::*, token::Token};

#[derive(Debug, Clone)]
pub struct Command {
    // TODO: fix hygiene
    pub tokens: Vec<Token>,
    pub flags: Vec<Flag>,
    pub help: SmolStr,
    pub cb: SmolStr,
    pub show_in_suggestions: bool,
    pub parse_flags_before: usize,
}

impl Command {
    pub fn new(tokens: impl IntoIterator<Item = Token>, cb: impl Into<SmolStr>) -> Self {
        let tokens = tokens.into_iter().collect::<Vec<_>>();
        assert!(tokens.len() > 0);
        // figure out which token to parse / put flags after
        // (by default, put flags after the last token)
        let mut parse_flags_before = tokens.len();
        let mut was_parameter = true;
        for (idx, token) in tokens.iter().enumerate().rev() {
            match token {
                // we want flags to go before any parameters
                Token::Parameter(_, _) | Token::Any(_) => {
                    parse_flags_before = idx;
                    was_parameter = true;
                }
                Token::Empty | Token::Value(_) => {
                    if was_parameter {
                        break;
                    }
                }
            }
        }
        Self {
            flags: Vec::new(),
            help: SmolStr::new_static("<no help text>"),
            cb: cb.into(),
            show_in_suggestions: true,
            parse_flags_before,
            tokens,
        }
    }

    pub fn help(mut self, v: impl Into<SmolStr>) -> Self {
        self.help = v.into();
        self
    }

    pub fn show_in_suggestions(mut self, v: bool) -> Self {
        self.show_in_suggestions = v;
        self
    }

    pub fn flag(mut self, name: impl Into<SmolStr>) -> Self {
        self.flags.push(Flag::new(name));
        self
    }

    pub fn value_flag(mut self, name: impl Into<SmolStr>, value: impl Parameter + 'static) -> Self {
        self.flags.push(Flag::new(name).with_value(value));
        self
    }
}

impl Display for Command {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        for (idx, token) in self.tokens.iter().enumerate() {
            if idx == self.parse_flags_before {
                for flag in &self.flags {
                    write!(f, "[{flag}] ")?;
                }
            }
            write!(
                f,
                "{token}{}",
                (idx < self.tokens.len() - 1).then_some(" ").unwrap_or("")
            )?;
        }
        if self.tokens.len() == self.parse_flags_before {
            for flag in &self.flags {
                write!(f, " [{flag}]")?;
            }
        }
        Ok(())
    }
}

// a macro is required because generic cant be different types at the same time (which means you couldnt have ["member", MemberRef, "subcmd"] etc)
// (and something like &dyn Trait would require everything to be referenced which doesnt look nice anyway)
#[macro_export]
macro_rules! command {
    ([$($v:expr),+], $cb:expr$(,)*) => {
        $crate::commands::Command::new([$(Token::from($v)),*], $cb)
    };
}

pub fn all() -> impl Iterator<Item = Command> {
    (help::cmds())
        .chain(system::cmds())
        .chain(member::cmds())
        .chain(config::cmds())
        .chain(fun::cmds())
}
