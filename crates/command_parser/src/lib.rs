#![feature(let_chains)]
#![feature(anonymous_lifetime_in_impl_trait)]

pub mod command;
mod flag;
pub mod parameter;
mod string;
pub mod token;
pub mod tree;

use core::panic;
use std::collections::HashMap;
use std::fmt::Write;
use std::ops::Not;

use command::Command;
use flag::{Flag, FlagMatchError, FlagValueMatchError};
use parameter::ParameterValue;
use smol_str::SmolStr;
use string::MatchedFlag;
use token::{Token, TokenMatchError, TokenMatchValue};

// todo: this should come from the bot probably
const MAX_SUGGESTIONS: usize = 7;

pub type Tree = tree::TreeBranch;

#[derive(Debug)]
pub struct ParsedCommand {
    pub command_def: Command,
    pub parameters: HashMap<String, ParameterValue>,
    pub flags: HashMap<String, Option<ParameterValue>>,
}

pub fn parse_command(
    command_tree: Tree,
    prefix: String,
    input: String,
) -> Result<ParsedCommand, String> {
    let input: SmolStr = input.into();
    let mut local_tree: Tree = command_tree.clone();

    // end position of all currently matched tokens
    let mut current_pos: usize = 0;
    let mut current_token_idx: usize = 0;

    let mut params: HashMap<String, ParameterValue> = HashMap::new();
    let mut raw_flags: Vec<(usize, MatchedFlag)> = Vec::new();

    loop {
        println!(
            "possible: {:?}",
            local_tree.possible_tokens().collect::<Vec<_>>()
        );
        let next = next_token(local_tree.possible_tokens(), &input, current_pos);
        println!("next: {:?}", next);
        match next {
            Some(Ok((found_token, arg, new_pos))) => {
                current_pos = new_pos;
                current_token_idx += 1;

                if let Some(arg) = arg.as_ref() {
                    // insert arg as paramater if this is a parameter
                    if let Some((param_name, param)) = arg.param.as_ref() {
                        params.insert(param_name.to_string(), param.clone());
                    }
                }

                if let Some(next_tree) = local_tree.get_branch(&found_token) {
                    local_tree = next_tree.clone();
                } else {
                    panic!("found token {found_token:?} could not match tree, at {input}");
                }
            }
            Some(Err((token, err))) => {
                let error_msg = match err {
                    TokenMatchError::MissingParameter { name } => {
                        format!("Expected parameter `{name}` in command `{prefix}{input} {token}`.")
                    }
                    TokenMatchError::ParameterMatchError { input: raw, msg } => {
                        format!("Parameter `{raw}` in command `{prefix}{input}` could not be parsed: {msg}.")
                    }
                };
                return Err(error_msg);
            }
            None => {
                // if it said command not found on a flag, output better error message
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
                return Err(error);
            }
        }
        // match flags until there are none left
        while let Some(matched_flag) = string::next_flag(&input, current_pos) {
            current_pos = matched_flag.next_pos;
            println!("flag matched {matched_flag:?}");
            raw_flags.push((current_token_idx, matched_flag));
        }
        // if we have a command, stop parsing and return it
        if let Some(command) = local_tree.command() {
            // match the flags against this commands flags
            let mut flags: HashMap<String, Option<ParameterValue>> = HashMap::new();
            let mut misplaced_flags: Vec<MatchedFlag> = Vec::new();
            let mut invalid_flags: Vec<MatchedFlag> = Vec::new();
            for (token_idx, matched_flag) in raw_flags {
                if token_idx != command.parse_flags_before {
                    misplaced_flags.push(matched_flag);
                    continue;
                }
                let Some(matched_flag) = match_flag(command.flags.iter(), matched_flag.clone())
                else {
                    invalid_flags.push(matched_flag);
                    continue;
                };
                match matched_flag {
                    // a flag was matched
                    Ok((name, value)) => {
                        flags.insert(name.into(), value);
                    }
                    Err((flag, err)) => {
                        let error = match err {
                            FlagMatchError::ValueMatchFailed(FlagValueMatchError::ValueMissing) => {
                                format!(
                                    "Flag `-{name}` in command `{prefix}{input}` is missing a value, try passing `{flag}`.",
                                    name = flag.name()
                                )
                            }
                            FlagMatchError::ValueMatchFailed(
                                FlagValueMatchError::InvalidValue { msg, raw },
                            ) => {
                                format!(
                                    "Flag `-{name}` in command `{prefix}{input}` has a value (`{raw}`) that could not be parsed: {msg}.",
                                    name = flag.name()
                                )
                            }
                        };
                        return Err(error);
                    }
                }
            }
            if misplaced_flags.is_empty().not() {
                let mut error = format!(
                    "Flag{} ",
                    (misplaced_flags.len() > 1).then_some("s").unwrap_or("")
                );
                for (idx, matched_flag) in misplaced_flags.iter().enumerate() {
                    write!(&mut error, "`-{}`", matched_flag.name).expect("oom");
                    if idx < misplaced_flags.len() - 1 {
                        error.push_str(", ");
                    }
                }
                write!(
                    &mut error,
                    " in command `{prefix}{input}` {} misplaced. Try reordering to match the command usage `{prefix}{command}`.",
                    (misplaced_flags.len() > 1).then_some("are").unwrap_or("is")
                ).expect("oom");
                return Err(error);
            }
            if invalid_flags.is_empty().not() {
                let mut error = format!(
                    "Flag{} ",
                    (misplaced_flags.len() > 1).then_some("s").unwrap_or("")
                );
                for (idx, matched_flag) in invalid_flags.iter().enumerate() {
                    write!(&mut error, "`-{}`", matched_flag.name).expect("oom");
                    if idx < invalid_flags.len() - 1 {
                        error.push_str(", ");
                    }
                }
                write!(
                    &mut error,
                    " {} not applicable in this command (`{prefix}{input}`). Applicable flags are the following:",
                    (invalid_flags.len() > 1).then_some("are").unwrap_or("is")
                ).expect("oom");
                for (idx, flag) in command.flags.iter().enumerate() {
                    write!(&mut error, " `{flag}`").expect("oom");
                    if idx < command.flags.len() - 1 {
                        error.push_str(", ");
                    }
                }
                error.push_str(".");
                return Err(error);
            }
            println!("{} {flags:?} {params:?}", command.cb);
            return Ok(ParsedCommand {
                command_def: command,
                flags,
                parameters: params,
            });
        }
    }
}

fn match_flag<'a>(
    possible_flags: impl Iterator<Item = &'a Flag>,
    matched_flag: MatchedFlag<'a>,
) -> Option<Result<(SmolStr, Option<ParameterValue>), (&'a Flag, FlagMatchError)>> {
    // check for all (possible) flags, see if token matches
    for flag in possible_flags {
        println!("matching flag {flag:?}");
        match flag.try_match(matched_flag.name, matched_flag.value) {
            Some(Ok(param)) => return Some(Ok((flag.name().into(), param))),
            Some(Err(err)) => return Some(Err((flag, err))),
            None => {}
        }
    }

    None
}

/// Find the next token from an either raw or partially parsed command string
///
/// Returns:
/// - nothing (none matched)
/// - matched token, to move deeper into the tree
/// - matched value (if this command matched an user-provided value such as a member name)
/// - end position of matched token
/// - error when matching
fn next_token<'a>(
    possible_tokens: impl Iterator<Item = &'a Token>,
    input: &str,
    current_pos: usize,
) -> Option<Result<(&'a Token, Option<TokenMatchValue>, usize), (&'a Token, TokenMatchError)>> {
    // get next parameter, matching quotes
    let matched = string::next_param(&input, current_pos);
    println!("matched: {matched:?}\n---");

    // iterate over tokens and run try_match
    for token in possible_tokens {
        let is_match_remaining_token =
            |token: &Token| matches!(token, Token::Parameter(param) if param.kind().remainder());
        // check if this is a token that matches the rest of the input
        let match_remaining = is_match_remaining_token(token);
        // either use matched param or rest of the input if matching remaining
        let input_to_match = matched.as_ref().map(|v| {
            match_remaining
                .then_some(&input[current_pos..])
                .unwrap_or(v.value)
        });
        match token.try_match(input_to_match) {
            Some(Ok(value)) => {
                println!("matched token: {}", token);
                let next_pos = match matched {
                    // return last possible pos if we matched remaining,
                    Some(_) if match_remaining => input.len(),
                    // otherwise use matched param next pos,
                    Some(param) => param.next_pos,
                    // and if didnt match anything we stay where we are
                    None => current_pos,
                };
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
        f.push_str(" Perhaps you meant one of the following commands:\n");
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
