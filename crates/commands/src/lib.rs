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
    pub args: Vec<String>,
    pub params: HashMap<String, Parameter>,
    pub flags: HashMap<String, Option<String>>,
}

pub fn parse_command(prefix: String, input: String) -> CommandResult {
    let input: SmolStr = input.into();
    let mut local_tree: TreeBranch = COMMAND_TREE.clone();

    // end position of all currently matched tokens
    let mut current_pos = 0;

    let mut args: Vec<String> = Vec::new();
    let mut params: HashMap<String, Parameter> = HashMap::new();
    let mut flags: HashMap<String, Option<String>> = HashMap::new();

    loop {
        let possible_tokens = local_tree.possible_tokens().cloned().collect::<Vec<_>>();
        println!("possible: {:?}", possible_tokens);
        let next = next_token(possible_tokens.clone(), input.clone(), current_pos);
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
                    args.push(arg.raw.to_string());
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
                            args,
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
    input: SmolStr,
    current_pos: usize,
) -> Option<Result<(Token, Option<TokenMatchedValue>, usize), (Token, TokenMatchError)>> {
    // get next parameter, matching quotes
    let param = crate::string::next_param(input.clone(), current_pos);
    println!("matched: {param:?}\n---");

    // try checking if this is a flag
    // todo!: this breaks full text matching if the full text starts with a flag
    // (but that's kinda already broken anyway)
    if let Some((value, new_pos)) = param.clone()
        && value.starts_with('-')
    {
        return Some(Ok((
            Token::Flag,
            Some(TokenMatchedValue {
                raw: value,
                param: None,
            }),
            new_pos,
        )));
    }

    // iterate over tokens and run try_match
    for token in possible_tokens {
        // for FullString just send the whole string
        let input_to_match = param.clone().map(|v| v.0);
        match token.try_match(input_to_match) {
            Some(Ok(value)) => {
                return Some(Ok((
                    token,
                    value,
                    param.map(|v| v.1).unwrap_or(current_pos),
                )))
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
