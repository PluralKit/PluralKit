use command_parser::token::TokensIterator;

use super::*;

pub fn group() -> (&'static str, [&'static str; 1]) {
    ("group", ["g"])
}

pub fn targeted() -> TokensIterator {
    tokens!(group(), GroupRef)
}
