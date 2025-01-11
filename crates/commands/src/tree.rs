use ordermap::OrderMap;

use crate::{commands::Command, Token};

#[derive(Debug, Clone)]
pub struct TreeBranch {
    current_command: Option<Command>,
    branches: OrderMap<Token, TreeBranch>,
}

impl TreeBranch {
    pub fn empty() -> Self {
        Self {
            current_command: None,
            branches: OrderMap::new(),
        }
    }

    pub fn register_command(&mut self, command: Command) {
        let mut current_branch = self;
        // iterate over tokens in command
        for token in command.tokens.clone() {
            // recursively get or create a sub-branch for each token
            current_branch = current_branch.branches.entry(token).or_insert(TreeBranch {
                current_command: None,
                branches: OrderMap::new(),
            })
        }
        // when we're out of tokens, add an Empty branch with the callback and no sub-branches
        current_branch.branches.insert(
            Token::Empty,
            TreeBranch {
                current_command: Some(command),
                branches: OrderMap::new(),
            },
        );
    }

    pub fn command(&self) -> Option<Command> {
        self.current_command.clone()
    }

    pub fn possible_tokens(&self) -> impl Iterator<Item = &Token> {
        self.branches.keys()
    }

    pub fn possible_commands(&self, max_depth: usize) -> Vec<Command> {
        if max_depth == 0 {
            return Vec::new();
        }
        let mut commands = Vec::new();
        for token in self.possible_tokens() {
            if let Some(tree) = self.get_branch(token) {
                if let Some(command) = tree.command() {
                    commands.push(command);
                    // we dont need to look further if we found a command
                    continue;
                }
                commands.append(&mut tree.possible_commands(max_depth - 1));
            }
        }
        commands
    }

    pub fn get_branch(&self, token: &Token) -> Option<&TreeBranch> {
        self.branches.get(token)
    }
}
