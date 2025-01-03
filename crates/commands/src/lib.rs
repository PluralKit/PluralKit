#![feature(let_chains)]

use core::panic;
use std::{cmp::Ordering, collections::HashMap};

uniffi::include_scaffolding!("commands");

mod string;
mod token;
use smol_str::SmolStr;
use token::*;

// todo!: move all this stuff into a different file
// lib.rs should just have exported symbols and command definitions

#[derive(Debug, Clone)]
struct TreeBranch {
    current_command_key: Option<String>,
    /// branches.keys(), but sorted by specificity
    possible_tokens: Vec<Token>,
    branches: HashMap<Token, TreeBranch>,
}

impl TreeBranch {
    fn register_command(&mut self, command: Command) {
        let mut current_branch = self;
        // iterate over tokens in command
        for token in command.tokens {
            // recursively get or create a sub-branch for each token
            current_branch = current_branch.branches.entry(token).or_insert(TreeBranch {
                current_command_key: None,
                possible_tokens: vec![],
                branches: HashMap::new(),
            })
        }
        // when we're out of tokens, add an Empty branch with the callback and no sub-branches
        current_branch.branches.insert(
            Token::Empty,
            TreeBranch {
                current_command_key: Some(command.cb),
                possible_tokens: vec![],
                branches: HashMap::new(),
            },
        );
    }

    fn sort_tokens(&mut self) {
        for branch in self.branches.values_mut() {
            branch.sort_tokens();
        }

        // put Value tokens at the end
        // i forget exactly how this works
        // todo!: document this before PR mergs
        self.possible_tokens = self
            .branches
            .keys()
            .into_iter()
            .map(|v| v.clone())
            .collect();
        self.possible_tokens.sort_by(|v, _| {
            if matches!(v, Token::Value(_)) {
                Ordering::Greater
            } else {
                Ordering::Less
            }
        });
    }
}

#[derive(Clone)]
struct Command {
    tokens: Vec<Token>,
    help: String,
    cb: String,
}

fn command(tokens: impl IntoIterator<Item = Token>, help: impl ToString, cb: impl ToString) -> Command {
    Command {
        tokens: tokens.into_iter().collect(),
        help: help.to_string(),
        cb: cb.to_string(),
    }
}

macro_rules! command {
    ([$($v:expr),+], $help:expr, $cb:expr) => {
        $crate::command([$($v.clone()),*], $help, $cb)
    };
}

mod commands {
    use smol_str::SmolStr;

    use super::Token;

    fn cmd(value: impl Into<SmolStr>) -> Token {
        Token::Value(vec![value.into()])
    }

    pub fn cmd_with_alias(value: impl IntoIterator<Item = impl Into<SmolStr>>) -> Token {
        Token::Value(value.into_iter().map(Into::into).collect())
    }

    // todo: this needs to have less ampersands -alyssa
    pub fn happy() -> Vec<super::Command> {
        use Token::*;

        let system = cmd_with_alias(["system", "s"]);
        let member = cmd_with_alias(["member", "m"]);
        let description = cmd_with_alias(["description", "desc"]);
        let privacy = cmd_with_alias(["privacy", "priv"]);
        vec![
            command!([cmd("help")], "help", "Shows the help command"),
            command!(
                [system],
                "system_show",
                "Shows information about your system"
            ),
            command!([system, cmd("new")], "system_new", "Creates a new system"),
            command!(
                [member, cmd_with_alias(["new", "n"])],
                "member_new",
                "Creates a new system member"
            ),
            command!(
                [member, MemberRef],
                "member_show",
                "Shows information about a member"
            ),
            command!(
                [member, MemberRef, description],
                "member_desc_show",
                "Shows a member's description"
            ),
            command!(
                [member, MemberRef, description, FullString],
                "member_desc_update",
                "Changes a member's description"
            ),
            command!(
                [member, MemberRef, privacy],
                "member_privacy_show",
                "Displays a member's current privacy settings"
            ),
            command!(
                [
                    member,
                    MemberRef,
                    privacy,
                    MemberPrivacyTarget,
                    PrivacyLevel
                ],
                "member_privacy_update",
                "Changes a member's privacy settings"
            ),
        ]
    }
}

lazy_static::lazy_static! {
    static ref COMMAND_TREE: TreeBranch = {
        let mut tree = TreeBranch {
            current_command_key: None,
            possible_tokens: vec![],
            branches: HashMap::new(),
        };

        commands::happy().iter().for_each(|x| tree.register_command(x.clone()));

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

fn parse_command(input: String) -> CommandResult {
    let input: SmolStr = input.into();
    let mut local_tree: TreeBranch = COMMAND_TREE.clone();

    // end position of all currently matched tokens
    let mut current_pos = 0;

    let mut args: Vec<String> = Vec::new();
    let mut flags: HashMap<String, Option<String>> = HashMap::new();

    loop {
        match next_token(
            local_tree.possible_tokens.clone(),
            input.clone(),
            current_pos,
        ) {
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
                            command_ref,
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
