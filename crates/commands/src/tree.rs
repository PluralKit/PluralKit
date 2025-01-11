use ordermap::OrderMap;
use smol_str::SmolStr;

use crate::{commands::Command, Token};

#[derive(Debug, Clone)]
pub struct TreeBranch {
    current_command_key: Option<SmolStr>,
    branches: OrderMap<Token, TreeBranch>,
}

impl TreeBranch {
    pub fn empty() -> Self {
        Self {
            current_command_key: None,
            branches: OrderMap::new(),
        }
    }

    pub fn register_command(&mut self, command: Command) {
        let mut current_branch = self;
        // iterate over tokens in command
        for token in command.tokens {
            // recursively get or create a sub-branch for each token
            current_branch = current_branch.branches.entry(token).or_insert(TreeBranch {
                current_command_key: None,
                branches: OrderMap::new(),
            })
        }
        // when we're out of tokens, add an Empty branch with the callback and no sub-branches
        current_branch.branches.insert(
            Token::Empty,
            TreeBranch {
                current_command_key: Some(command.cb),
                branches: OrderMap::new(),
            },
        );
    }

    pub fn callback(&self) -> Option<SmolStr> {
        self.current_command_key.clone()
    }

    pub fn possible_tokens(&self) -> impl Iterator<Item = &Token> {
        self.branches.keys()
    }

    pub fn get_branch(&self, token: &Token) -> Option<&TreeBranch> {
        self.branches.get(token)
    }
}
