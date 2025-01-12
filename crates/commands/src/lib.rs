#![feature(let_chains)]
#![feature(anonymous_lifetime_in_impl_trait)]

pub mod commands;
mod string;
mod token;
mod tree;

uniffi::include_scaffolding!("commands");

use core::panic;
use std::collections::HashMap;
use std::fmt::Write;
use std::ops::Not;

use smol_str::SmolStr;
use tree::TreeBranch;

pub use commands::Command;
pub use token::*;

// todo: this should come from the bot probably
const MAX_SUGGESTIONS: usize = 7;

lazy_static::lazy_static! {
    pub static ref COMMAND_TREE: TreeBranch = {
        let mut tree = TreeBranch::empty();

        crate::commands::all().into_iter().for_each(|x| tree.register_command(x));

        tree
    };
}

#[derive(Debug)]
pub enum CommandResult {
    Ok { command: ParsedCommand },
    Err { error: String },
}

#[derive(Debug, Clone)]
pub enum Parameter {
    MemberRef { member: String },
    SystemRef { system: String },
    MemberPrivacyTarget { target: String },
    PrivacyLevel { level: String },
    OpaqueString { raw: String },
    Toggle { toggle: bool },
    Reset,
}

#[derive(Debug)]
pub struct ParsedCommand {
    pub command_ref: String,
    pub params: HashMap<String, Parameter>,
    pub flags: HashMap<String, Option<String>>,
}

pub fn parse_command(prefix: String, input: String) -> CommandResult {
    let input: SmolStr = input.into();
    let mut local_tree: TreeBranch = COMMAND_TREE.clone();

    // end position of all currently matched tokens
    let mut current_pos = 0;

    let mut params: HashMap<String, Parameter> = HashMap::new();
    let mut flags: HashMap<String, Option<String>> = HashMap::new();

    loop {
        let possible_tokens = local_tree.possible_tokens().cloned().collect::<Vec<_>>();
        println!("possible: {:?}", possible_tokens);
        let next = next_token(possible_tokens.clone(), &input, current_pos);
        println!("next: {:?}", next);
        match next {
            Some(Ok((found_token, arg, new_pos))) => {
                current_pos = new_pos;
                if let Token::Flag = found_token {
                    flags.insert(arg.unwrap().raw.into(), None);
                    // don't try matching flags as tree elements
                    continue;
                }

                if let Some(arg) = arg.as_ref() {
                    // insert arg as paramater if this is a parameter
                    if let Some((param_name, param)) = arg.param.as_ref() {
                        params.insert(param_name.to_string(), param.clone());
                    }
                }

                if let Some(next_tree) = local_tree.get_branch(&found_token) {
                    local_tree = next_tree.clone();
                } else {
                    panic!("found token could not match tree, at {input}");
                }
            }
            Some(Err((token, err))) => {
                let error_msg = match err {
                    TokenMatchError::MissingParameter { name } => {
                        format!("Expected parameter `{name}` in command `{prefix}{input} {token}`.")
                    }
                    TokenMatchError::MissingAny { tokens } => {
                        let mut msg = format!("Expected one of ");
                        for (idx, token) in tokens.iter().enumerate() {
                            write!(&mut msg, "`{token}`").expect("oom");
                            if idx < tokens.len() - 1 {
                                if tokens.len() > 2 && idx == tokens.len() - 2 {
                                    msg.push_str(" or ");
                                } else {
                                    msg.push_str(", ");
                                }
                            }
                        }
                        write!(&mut msg, " in command `{prefix}{input} {token}`.").expect("oom");
                        msg
                    }
                };
                return CommandResult::Err { error: error_msg };
            }
            None => {
                if let Some(command) = local_tree.command() {
                    println!("{} {params:?}", command.cb);
                    return CommandResult::Ok {
                        command: ParsedCommand {
                            command_ref: command.cb.into(),
                            params,
                            flags,
                        },
                    };
                }

                let mut error = format!("Unknown command `{prefix}{input}`.");

                if fmt_possible_commands(&mut error, &prefix, local_tree.possible_commands(2)).not()
                {
                    error.push_str(" ");
                }

                error.push_str(
                    "For a list of all possible commands, see <https://pluralkit.me/commands>.",
                );

                // todo: check if last token is a common incorrect unquote (multi-member names etc)
                // todo: check if this is a system name in pk;s command
                return CommandResult::Err { error };
            }
        }
    }
}

/// Find the next token from an either raw or partially parsed command string
///
/// Returns:
/// - nothing (none matched)
/// - matched token, to move deeper into the tree
/// - matched value (if this command matched an user-provided value such as a member name)
/// - end position of matched token
/// - error when matching
fn next_token(
    possible_tokens: Vec<Token>,
    input: &str,
    current_pos: usize,
) -> Option<Result<(Token, Option<TokenMatchedValue>, usize), (Token, TokenMatchError)>> {
    // get next parameter, matching quotes
    let matched = crate::string::next_param(&input, current_pos);
    println!("matched: {matched:?}\n---");

    // try checking if this is a flag
    // note: if the param starts with - and if a "match remainder" token was going to be matched
    // this is going to override that. to prevent that the param should be quoted
    if let Some(param) = matched.as_ref()
        && param.in_quotes.not()
        && param.value.starts_with('-')
    {
        return Some(Ok((
            Token::Flag,
            Some(TokenMatchedValue {
                raw: param.value.into(),
                param: None,
            }),
            param.next_pos,
        )));
    }

    // iterate over tokens and run try_match
    for token in possible_tokens {
        let is_match_remaining_token = |token: &Token| matches!(token, Token::FullString(_));
        // check if this is a token that matches the rest of the input
        let match_remaining = is_match_remaining_token(&token)
            // check for Any here if it has a "match remainder" token in it
            // if there is a "match remainder" token in a command there shouldn't be a command descending from that
            || matches!(token, Token::Any(ref tokens) if tokens.iter().any(is_match_remaining_token));
        // either use matched param or rest of the input if matching remaining
        let input_to_match = matched.as_ref().map(|v| {
            match_remaining
                .then_some(&input[current_pos..])
                .unwrap_or(v.value)
        });
        match token.try_match(input_to_match) {
            Some(Ok(value)) => {
                // return last possible pos if we matched remaining,
                // otherwise use matched param next pos,
                // and if didnt match anything we stay where we are
                let next_pos = matched
                    .map(|v| match_remaining.then_some(input.len()).unwrap_or(v.next_pos))
                    .unwrap_or(current_pos);
                return Some(Ok((token, value, next_pos)));
            }
            Some(Err(err)) => {
                return Some(Err((token, err)));
            }
            None => {} // continue matching until we exhaust all tokens
        }
    }

    None
}

// todo: should probably move this somewhere else
/// returns true if wrote possible commands, false if not
fn fmt_possible_commands(
    f: &mut String,
    prefix: &str,
    mut possible_commands: impl Iterator<Item = &Command>,
) -> bool {
    if let Some(first) = possible_commands.next() {
        f.push_str(" Perhaps you meant to use one of the commands below:\n");
        for command in std::iter::once(first).chain(possible_commands.take(MAX_SUGGESTIONS - 1)) {
            if !command.show_in_suggestions {
                continue;
            }
            writeln!(f, "- **{prefix}{command}** - *{}*", command.help).expect("oom");
        }
        return true;
    }
    return false;
}
