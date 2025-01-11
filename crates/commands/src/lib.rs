#![feature(let_chains)]

pub mod commands;
mod string;
mod token;
mod tree;

uniffi::include_scaffolding!("commands");

use core::panic;
use std::collections::HashMap;

use smol_str::{format_smolstr, SmolStr};
use tree::TreeBranch;

pub use commands::Command;
pub use token::*;

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

pub fn parse_command(input: String) -> CommandResult {
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
            Ok((found_token, arg, new_pos)) => {
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
            Err(None) => {
                if let Some(command_ref) = local_tree.callback() {
                    println!("{command_ref} {params:?}");
                    return CommandResult::Ok {
                        command: ParsedCommand {
                            command_ref: command_ref.into(),
                            params,
                            args,
                            flags,
                        },
                    };
                }

                println!("{possible_tokens:?}");

                // todo: check if last token is a common incorrect unquote (multi-member names etc)
                // todo: check if this is a system name in pk;s command
                return CommandResult::Err {
                    error: format!("Unknown command `{input}`. For a list of possible commands, see <https://pluralkit.me/commands>."),
                };
            }
            Err(Some(short_circuit)) => {
                return CommandResult::Err {
                    error: short_circuit.into(),
                };
            }
        }
    }
}

/// Find the next token from an either raw or partially parsed command string
///
/// Returns:
/// - matched token, to move deeper into the tree
/// - matched value (if this command matched an user-provided value such as a member name)
/// - end position of matched token
/// - optionally a short-circuit error
fn next_token(
    possible_tokens: Vec<Token>,
    input: SmolStr,
    current_pos: usize,
) -> Result<(Token, Option<TokenMatchedValue>, usize), Option<SmolStr>> {
    // get next parameter, matching quotes
    let param = crate::string::next_param(input.clone(), current_pos);
    println!("matched: {param:?}\n---");

    // try checking if this is a flag
    // todo!: this breaks full text matching if the full text starts with a flag
    // (but that's kinda already broken anyway)
    if let Some((value, new_pos)) = param.clone()
        && value.starts_with('-')
    {
        return Ok((
            Token::Flag,
            Some(TokenMatchedValue {
                raw: value,
                param: None,
            }),
            new_pos,
        ));
    }

    // iterate over tokens and run try_match
    for token in possible_tokens {
        // for FullString just send the whole string
        let input_to_match = param.clone().map(|v| v.0);
        match token.try_match(input_to_match) {
            TokenMatchResult::Match(value) => {
                return Ok((token, value, param.map(|v| v.1).unwrap_or(current_pos)))
            }
            TokenMatchResult::MissingParameter { name } => {
                return Err(Some(format_smolstr!(
                    "Missing parameter `{name}` in command `{input} [{name}]`."
                )))
            }
            TokenMatchResult::NoMatch => {}
        }
    }

    Err(None)
}
