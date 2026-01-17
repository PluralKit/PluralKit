use ordermap::OrderMap;

use crate::{command::Command, token::Token};

#[derive(Debug, Clone)]
pub struct TreeBranch {
    current_command: Option<Command>,
    branches: OrderMap<Token, TreeBranch>,
}

impl Default for TreeBranch {
    fn default() -> Self {
        Self {
            current_command: None,
            branches: OrderMap::new(),
        }
    }
}

impl TreeBranch {
    pub fn register_command(&mut self, command: Command) {
        let mut current_branch = self;
        // iterate over tokens in command
        for (index, token) in command.tokens.clone().into_iter().enumerate() {
            // if the token is an optional parameter, register rest of the tokens to a separate branch
            // this allows optional parameters to work if they are not the last token
            if matches!(token, Token::Parameter(ref param) if param.is_optional())
                && index < command.tokens.len() - 1
            {
                current_branch.register_command(Command {
                    tokens: command.tokens[index + 1..].to_vec(),
                    ..command.clone()
                });
            }
            // recursively get or create a sub-branch for each token
            current_branch = current_branch
                .branches
                .entry(token)
                .or_insert_with(TreeBranch::default);
        }
        // when we're out of tokens add the command to the last branch
        current_branch.current_command = Some(command);
    }

    pub fn command(&self) -> Option<Command> {
        self.current_command.clone()
    }

    pub fn possible_tokens(&self) -> impl Iterator<Item = &Token> + Clone {
        self.branches.keys()
    }

    pub fn possible_commands(&self, max_depth: usize) -> impl Iterator<Item = &Command> {
        // dusk: i am too lazy to write an iterator for this without using recursion so we box everything
        fn box_iter<'a>(
            iter: impl Iterator<Item = &'a Command> + 'a,
        ) -> Box<dyn Iterator<Item = &'a Command> + 'a> {
            Box::new(iter)
        }

        if max_depth == 0 {
            return box_iter(std::iter::empty());
        }
        let mut commands = box_iter(std::iter::empty());
        for branch in self.branches.values() {
            if let Some(command) = branch.current_command.as_ref() {
                commands = box_iter(commands.chain(std::iter::once(command)));
                // we dont need to look further if we found a command
                continue;
            }
            commands = box_iter(commands.chain(branch.possible_commands(max_depth - 1)));
        }
        commands
    }

    pub fn get_branch(&self, token: &Token) -> Option<&Self> {
        self.branches.get(token)
    }

    pub fn branches(&self) -> impl Iterator<Item = (&Token, &Self)> {
        self.branches.iter()
    }
}
