#![feature(let_chains)]

pub mod commands;
mod string;
mod token;
mod tree;

uniffi::include_scaffolding!("commands");

use core::panic;
use std::collections::HashMap;

use smol_str::SmolStr;
use tree::TreeBranch;

pub use commands::Command;
pub use token::*;

lazy_static::lazy_static! {
    pub static ref COMMAND_TREE: TreeBranch = {
        let mut tree = TreeBranch {
            current_command_key: None,
            possible_tokens: vec![],
            branches: HashMap::new(),
        };

        crate::commands::all().iter().for_each(|x| tree.register_command(x.clone()));

        tree.sort_tokens();

        // println!("{{tree:#?}}");

        tree
    };
}

pub enum CommandResult {
    Ok { command: ParsedCommand },
    Err { error: String },
}

pub struct ParsedCommand {
    pub command_ref: String,
    pub args: Vec<String>,
    pub flags: HashMap<String, Option<String>>,
}

fn parse_command(input: String) -> CommandResult {
    let input: SmolStr = input.into();
    let mut local_tree: TreeBranch = COMMAND_TREE.clone();

    // end position of all currently matched tokens
    let mut current_pos = 0;

    let mut args: Vec<String> = Vec::new();
    let mut flags: HashMap<String, Option<String>> = HashMap::new();

    loop {
        let next = next_token(
            local_tree.possible_tokens.clone(),
            input.clone(),
            current_pos,
        );
        match next {
            Ok((found_token, arg, new_pos)) => {
                current_pos = new_pos;
                if let Token::Flag = found_token {
                    flags.insert(arg.unwrap().into(), None);
                    // don't try matching flags as tree elements
                    continue;
                }

                if let Some(arg) = arg {
                    args.push(arg.into());
                }

                if let Some(next_tree) = local_tree.branches.get(&found_token) {
                    local_tree = next_tree.clone();
                } else {
                    panic!("found token could not match tree, at {input}");
                }
            }
            Err(None) => {
                if let Some(command_ref) = local_tree.current_command_key {
                    return CommandResult::Ok {
                        command: ParsedCommand {
                            command_ref: command_ref.into(),
                            args,
                            flags,
                        },
                    };
                }
                // todo: check if last token is a common incorrect unquote (multi-member names etc)
                // todo: check if this is a system name in pk;s command
                return CommandResult::Err {
                    error: "Command not found.".to_string(),
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
) -> Result<(Token, Option<SmolStr>, usize), Option<SmolStr>> {
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
            Some(value.trim_start_matches('-').into()),
            new_pos,
        ));
    }

    // iterate over tokens and run try_match
    for token in possible_tokens {
        if let TokenMatchResult::Match(value) =
            // for FullString just send the whole string
            token.try_match(if matches!(token, Token::FullString) {
                    if input.is_empty() {
                        None
                    } else {
                        Some(input.clone())
                    }
                } else {
                    param.clone().map(|v| v.0)
                })
        {
            return Ok((token, value, param.map(|v| v.1).unwrap_or(current_pos)));
        }
    }

    Err(None)
}
