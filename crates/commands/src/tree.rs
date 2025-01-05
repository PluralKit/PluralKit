use ordermap::OrderMap;
use smol_str::SmolStr;

use crate::{commands::Command, Token};
use std::cmp::Ordering;

#[derive(Debug, Clone)]
pub struct TreeBranch {
    pub current_command_key: Option<SmolStr>,
    /// branches.keys(), but sorted by specificity
    pub possible_tokens: Vec<Token>,
    pub branches: OrderMap<Token, TreeBranch>,
}

impl TreeBranch {
    pub fn register_command(&mut self, command: Command) {
        let mut current_branch = self;
        // iterate over tokens in command
        for token in command.tokens {
            // recursively get or create a sub-branch for each token
            current_branch = current_branch.branches.entry(token).or_insert(TreeBranch {
                current_command_key: None,
                possible_tokens: vec![],
                branches: OrderMap::new(),
            })
        }
        // when we're out of tokens, add an Empty branch with the callback and no sub-branches
        current_branch.branches.insert(
            Token::Empty,
            TreeBranch {
                current_command_key: Some(command.cb),
                possible_tokens: vec![],
                branches: OrderMap::new(),
            },
        );
    }
}
