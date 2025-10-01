use std::fmt::{Debug, Display};

use smol_str::SmolStr;

use crate::parameter::{Parameter, ParameterKind, ParameterValue};

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum Token {
    /// A bot-defined command / subcommand (usually) (eg. "member" in `pk;member MyName`)
    Value {
        name: SmolStr,
        aliases: Vec<SmolStr>,
    },

    /// A parameter that must be provided a value
    Parameter(Parameter),
}

#[derive(Debug)]
pub enum TokenMatchResult {
    MatchedValue,
    MatchedParameter {
        name: SmolStr,
        value: ParameterValue,
    },
    ParameterMatchError {
        input: SmolStr,
        msg: SmolStr,
    },
    MissingParameter {
        name: SmolStr,
    },
}

// q: why not have a NoMatch variant in TokenMatchResult?
// a: because we want to differentiate between no match and match failure (it matched with an error)
//    "no match" has a different charecteristic because we want to continue matching other tokens...
//    ...while "match failure" means we should stop matching and return the error
type TryMatchResult = Option<TokenMatchResult>;

impl Token {
    pub(super) fn try_match(&self, input: Option<&str>) -> TryMatchResult {
        let input = match input {
            Some(input) => input,
            None => {
                // short circuit on:
                return match self {
                    // missing paramaters
                    Self::Parameter(param) => Some(TokenMatchResult::MissingParameter {
                        name: param.name().into(),
                    }),
                    // everything else doesnt match if no input anyway
                    Self::Value { .. } => None,
                    // don't add a _ match here!
                };
            }
        };
        let input = input.trim();

        // try actually matching stuff
        match self {
            Self::Value { name, aliases } => (aliases.iter().chain(std::iter::once(name)))
                .any(|v| v.eq(input))
                .then(|| TokenMatchResult::MatchedValue),
            Self::Parameter(param) => Some(match param.kind().match_value(input) {
                Ok(matched) => TokenMatchResult::MatchedParameter {
                    name: param.name().into(),
                    value: matched,
                },
                Err(err) => {
                    if let Some(maybe_empty) = param.kind().skip_if_cant_match() {
                        match maybe_empty {
                            Some(matched) => TokenMatchResult::MatchedParameter {
                                name: param.name().into(),
                                value: matched,
                            },
                            None => return None,
                        }
                    } else {
                        TokenMatchResult::ParameterMatchError {
                            input: input.into(),
                            msg: err,
                        }
                    }
                }
            }),
            // don't add a _ match here!
        }
    }
}

impl Display for Token {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Value { name, .. } => write!(f, "{name}"),
            Self::Parameter(param) => write!(f, "{param}"),
        }
    }
}

// (name, aliases) -> Token::Value
impl<const L: usize> From<(&str, [&str; L])> for Token {
    fn from((name, aliases): (&str, [&str; L])) -> Self {
        Self::Value {
            name: name.into(),
            aliases: aliases.into_iter().map(SmolStr::new).collect::<Vec<_>>(),
        }
    }
}

// name -> Token::Value
impl From<&str> for Token {
    fn from(value: &str) -> Self {
        Self::from((value, []))
    }
}

impl From<Parameter> for Token {
    fn from(value: Parameter) -> Self {
        Self::Parameter(value)
    }
}

impl From<ParameterKind> for Token {
    fn from(value: ParameterKind) -> Self {
        Self::from(Parameter::from(value))
    }
}

impl From<(&str, ParameterKind)> for Token {
    fn from(value: (&str, ParameterKind)) -> Self {
        Self::from(Parameter::from(value))
    }
}

/// Iterator that produces [`Token`]s.
///
/// This is more of a convenience type that the [`tokens!`] macro uses in order
/// to more easily combine tokens together.
#[derive(Debug, Clone)]
pub struct TokensIterator {
    inner: Vec<Token>,
}

impl TokensIterator {
    pub(crate) fn new(tokens: Vec<Token>) -> Self {
        Self { inner: tokens }
    }
}

impl Iterator for TokensIterator {
    type Item = Token;

    fn next(&mut self) -> Option<Self::Item> {
        (self.inner.len() > 0).then(|| self.inner.remove(0))
    }
}

impl From<Vec<Token>> for TokensIterator {
    fn from(value: Vec<Token>) -> Self {
        Self::new(value)
    }
}

impl<T: Into<Token>> From<T> for TokensIterator {
    fn from(value: T) -> Self {
        Self::new(vec![value.into()])
    }
}

impl<const L: usize> From<[Token; L]> for TokensIterator {
    fn from(value: [Token; L]) -> Self {
        Self::new(value.into_iter().collect())
    }
}

impl<const L: usize> From<[Self; L]> for TokensIterator {
    fn from(value: [Self; L]) -> Self {
        Self::new(value.into_iter().map(|t| t.inner).flatten().collect())
    }
}

#[macro_export]
macro_rules! tokens {
    ($($v:expr),+$(,)*) => {
        $crate::token::TokensIterator::from([$($crate::token::TokensIterator::from($v.clone())),+])
    };
}
