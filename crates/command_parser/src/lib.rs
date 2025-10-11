#![feature(anonymous_lifetime_in_impl_trait)]
#![feature(round_char_boundary)]

pub mod command;
pub mod flag;
pub mod parameter;
mod string;
pub mod token;
pub mod tree;

use core::panic;
use std::fmt::Write;
use std::ops::Not;
use std::{collections::HashMap, usize};

use command::Command;
use flag::{Flag, FlagMatchError, FlagValueMatchError};
use parameter::ParameterValue;
use smol_str::SmolStr;
use string::MatchedFlag;
use token::{Token, TokenMatchResult};

// todo: this should come from the bot probably
const MAX_SUGGESTIONS: usize = 5;

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
            Some((found_token, result, new_pos)) => {
                match &result {
                    // todo: better error messages for these?
                    TokenMatchResult::MissingParameter { name } => {
                        return Err(format!(
                            "Expected parameter `{name}` in command `{prefix}{input} {found_token}`."
                        ));
                    }
                    TokenMatchResult::ParameterMatchError { input: raw, msg } => {
                        return Err(format!(
                            "Parameter `{raw}` in command `{prefix}{input}` could not be parsed: {msg}."
                        ));
                    }
                    // don't use a catch-all here, we want to make sure compiler errors when new errors are added
                    TokenMatchResult::MatchedParameter { .. } | TokenMatchResult::MatchedValue => {}
                }

                // add parameter if any
                if let TokenMatchResult::MatchedParameter { name, value } = result {
                    params.insert(name.to_string(), value);
                }

                // move to the next branch
                if let Some(next_tree) = local_tree.get_branch(&found_token) {
                    local_tree = next_tree.clone();
                } else {
                    panic!("found token {found_token:?} could not match tree, at {input}");
                }

                // advance our position on the input
                current_pos = new_pos;
                current_token_idx += 1;
            }
            None => {
                let mut error = format!("Unknown command `{prefix}{input}`.");

                let possible_commands =
                    rank_possible_commands(&input, local_tree.possible_commands(usize::MAX));
                if possible_commands.is_empty().not() {
                    error.push_str(" Perhaps you meant one of the following commands:\n");
                    fmt_commands_list(&mut error, &prefix, possible_commands);
                } else {
                    // add a space between the unknown command and "for a list of all possible commands"
                    // message if we didn't add any possible suggestions
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
        // if we have a command, stop parsing and return it (only if there is no remaining input)
        if current_pos >= input.len()
            && let Some(command) = local_tree.command()
        {
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
                                    name = flag.get_name()
                                )
                            }
                            FlagMatchError::ValueMatchFailed(
                                FlagValueMatchError::InvalidValue { msg, raw },
                            ) => {
                                format!(
                                    "Flag `-{name}` in command `{prefix}{input}` has a value (`{raw}`) that could not be parsed: {msg}.",
                                    name = flag.get_name()
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
                    (invalid_flags.len() > 1).then_some("s").unwrap_or("")
                );
                for (idx, matched_flag) in invalid_flags.iter().enumerate() {
                    write!(&mut error, "`-{}`", matched_flag.name).expect("oom");
                    if idx < invalid_flags.len() - 1 {
                        error.push_str(", ");
                    }
                }
                write!(
                    &mut error,
                    " {} seem to be applicable in this command (`{prefix}{command}`).",
                    (invalid_flags.len() > 1)
                        .then_some("don't")
                        .unwrap_or("doesn't")
                )
                .expect("oom");
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
            Some(Ok(param)) => return Some(Ok((flag.get_name().into(), param))),
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
) -> Option<(&'a Token, TokenMatchResult, usize)> {
    // get next parameter, matching quotes
    let matched = string::next_param(&input, current_pos);
    println!("matched: {matched:?}\n---");

    // iterate over tokens and run try_match
    for token in possible_tokens {
        let is_match_remaining_token =
            |token: &Token| matches!(token, Token::Parameter(param) if param.is_remainder());
        // check if this is a token that matches the rest of the input
        let match_remaining = is_match_remaining_token(token);
        // either use matched param or rest of the input if matching remaining
        let input_to_match = matched.as_ref().map(|v| {
            match_remaining
                .then_some(&input[current_pos..])
                .unwrap_or(v.value)
        });
        let next_pos = match matched {
            // return last possible pos if we matched remaining,
            Some(_) if match_remaining => input.len(),
            // otherwise use matched param next pos,
            Some(ref param) => param.next_pos,
            // and if didnt match anything we stay where we are
            None => current_pos,
        };
        match token.try_match(input_to_match) {
            Some(result) => {
                //println!("matched token: {}", token);
                return Some((token, result, next_pos));
            }
            None => {} // continue matching until we exhaust all tokens
        }
    }

    None
}

// todo: should probably move this somewhere else
/// returns true if wrote possible commands, false if not
fn rank_possible_commands(
    input: &str,
    possible_commands: impl IntoIterator<Item = &Command>,
) -> Vec<(Command, String, bool)> {
    let mut commands_with_scores: Vec<(&Command, String, f64, bool)> = possible_commands
        .into_iter()
        .filter(|cmd| cmd.show_in_suggestions)
        .flat_map(|cmd| {
            let versions = generate_command_versions(cmd);
            versions.into_iter().map(move |(version, is_alias)| {
                let similarity = strsim::jaro_winkler(&input, &version);
                (cmd, version, similarity, is_alias)
            })
        })
        .collect();

    commands_with_scores.sort_by(|a, b| b.2.partial_cmp(&a.2).unwrap_or(std::cmp::Ordering::Equal));

    // remove duplicate commands
    let mut seen_commands = std::collections::HashSet::new();
    let mut best_commands = Vec::new();
    for (cmd, version, score, is_alias) in commands_with_scores {
        if seen_commands.insert(cmd) {
            best_commands.push((cmd, version, score, is_alias));
        }
    }

    const MIN_SCORE_THRESHOLD: f64 = 0.8;
    if best_commands.is_empty() || best_commands[0].2 < MIN_SCORE_THRESHOLD {
        return Vec::new();
    }

    // if score falls off too much, don't show
    let mut falloff_threshold: f64 = 0.2;
    let best_score = best_commands[0].2;

    let mut commands_to_show = Vec::new();
    for (command, version, score, is_alias) in best_commands.into_iter().take(MAX_SUGGESTIONS) {
        let delta = best_score - score;
        falloff_threshold -= delta;
        if delta > falloff_threshold {
            break;
        }
        commands_to_show.push((command.clone(), version, is_alias));
    }

    commands_to_show
}

fn fmt_commands_list(f: &mut String, prefix: &str, commands_to_show: Vec<(Command, String, bool)>) {
    for (command, version, is_alias) in commands_to_show {
        writeln!(
            f,
            "- **{prefix}{version}**{alias} - *{help}*",
            help = command.help,
            alias = is_alias
                .then(|| format!(
                    " (alias of **{prefix}{base_version}**)",
                    base_version = build_command_string(&command, None)
                ))
                .unwrap_or_else(String::new),
        )
        .expect("oom");
    }
}

fn generate_command_versions(cmd: &Command) -> Vec<(String, bool)> {
    let mut versions = Vec::new();

    // Start with base version using primary names
    let base_version = build_command_string(cmd, None);
    versions.push((base_version, false));

    // Generate versions for each alias combination
    for (idx, token) in cmd.tokens.iter().enumerate() {
        if let Token::Value { aliases, .. } = token {
            for alias in aliases {
                versions.push((build_command_string(cmd, Some((idx, alias.as_str()))), true));
            }
        }
    }

    versions
}

fn build_command_string(cmd: &Command, alias_replacement: Option<(usize, &str)>) -> String {
    let mut result = String::new();
    for (idx, token) in cmd.tokens.iter().enumerate() {
        if idx > 0 {
            result.push(' ');
        }

        // Check if we should use an alias for this token
        let replacement = alias_replacement
            .filter(|(i, _)| *i == idx)
            .map(|(_, alias)| alias);

        match token {
            Token::Value { name, .. } => {
                result.push_str(replacement.unwrap_or(name));
            }
            Token::Parameter(param) => write!(&mut result, "{param}").unwrap(),
        }
    }
    result
}
