use std::{
    collections::HashSet,
    fmt::{Debug, Display},
};

use smol_str::SmolStr;

use crate::{flag::Flag, token::Token};

#[derive(Debug, Clone)]
pub struct Command {
    // TODO: fix hygiene
    pub tokens: Vec<Token>,
    pub flags: HashSet<Flag>,
    pub help: SmolStr,
    pub cb: SmolStr,
    pub show_in_suggestions: bool,
    pub parse_flags_before: usize,
    pub hidden_flags: HashSet<SmolStr>,
}

impl Command {
    pub fn new(tokens: impl IntoIterator<Item = Token>, cb: impl Into<SmolStr>) -> Self {
        let tokens = tokens.into_iter().collect::<Vec<_>>();
        assert!(tokens.len() > 0);
        // figure out which token to parse / put flags after
        // (by default, put flags after the last token)
        let mut parse_flags_before = tokens.len();
        for (idx, token) in tokens.iter().enumerate().rev() {
            match token {
                // we want flags to go before any parameters
                Token::Parameter(_) => parse_flags_before = idx,
                Token::Value { .. } => break,
            }
        }
        Self {
            flags: HashSet::new(),
            help: SmolStr::new_static("<no help text>"),
            cb: cb.into(),
            show_in_suggestions: true,
            parse_flags_before,
            tokens,
            hidden_flags: HashSet::new(),
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

    pub fn flags(mut self, flags: impl IntoIterator<Item = impl Into<Flag>>) -> Self {
        self.flags.extend(flags.into_iter().map(Into::into));
        self
    }

    pub fn flag(mut self, flag: impl Into<Flag>) -> Self {
        self.flags.insert(flag.into());
        self
    }

    pub fn hidden_flag(mut self, flag: impl Into<Flag>) -> Self {
        let flag = flag.into();
        self.hidden_flags.insert(flag.get_name().into());
        self.flags.insert(flag);
        self
    }
}

impl Display for Command {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let visible_flags = self
            .flags
            .iter()
            .filter(|flag| !self.hidden_flags.contains(flag.get_name()))
            .collect::<Vec<_>>();
        let write_flags = |f: &mut std::fmt::Formatter<'_>, space: bool| {
            if visible_flags.is_empty() {
                return Ok(());
            }
            write!(f, "{}(", space.then_some(" ").unwrap_or(""))?;
            let mut written = 0;
            let max_flags = visible_flags.len().min(5);
            for flag in &visible_flags {
                if written > max_flags {
                    break;
                }
                write!(f, "{flag}")?;
                if max_flags - 1 > written {
                    write!(f, " ")?;
                }
                written += 1;
            }
            if visible_flags.len() > written {
                let rest_count = visible_flags.len() - written;
                write!(
                    f,
                    " ...and {rest_count} flag{}...",
                    (rest_count > 1).then_some("s").unwrap_or(""),
                )?;
            }
            write!(f, "){}", space.then_some("").unwrap_or(" "))
        };

        for (idx, token) in self.tokens.iter().enumerate() {
            if idx == self.parse_flags_before {
                write_flags(f, false)?;
            }
            write!(
                f,
                "{token}{}",
                (idx < self.tokens.len() - 1).then_some(" ").unwrap_or("")
            )?;
        }
        if self.tokens.len() == self.parse_flags_before {
            write_flags(f, true)?;
        }
        Ok(())
    }
}

// a macro is required because generic cant be different types at the same time (which means you couldnt have ["member", MemberRef, "subcmd"] etc)
// (and something like &dyn Trait would require everything to be referenced which doesnt look nice anyway)
#[macro_export]
macro_rules! command {
    ($($v:expr),+ => $cb:expr$(,)*) => {
        $crate::command::Command::new($crate::tokens!($($v),+), $cb)
    };
}
