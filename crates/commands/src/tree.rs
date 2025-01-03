use crate::{commands::Command, Token};
use std::{cmp::Ordering, collections::HashMap};

#[derive(Debug, Clone)]
pub struct TreeBranch {
    pub current_command_key: Option<String>,
    /// branches.keys(), but sorted by specificity
    pub possible_tokens: Vec<Token>,
    pub branches: HashMap<Token, TreeBranch>,
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

    pub fn sort_tokens(&mut self) {
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
