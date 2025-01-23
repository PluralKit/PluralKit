use std::fmt::{Debug, Display};

use smol_str::SmolStr;

use crate::parameter::{Parameter, ParameterKind, ParameterValue};

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum Token {
    /// Token used to represent a finished command (i.e. no more parameters required)
    // todo: this is likely not the right way to represent this
    Empty,

    /// A bot-defined command / subcommand (usually) (eg. "member" in `pk;member MyName`)
    Value {
        name: SmolStr,
        aliases: Vec<SmolStr>,
    },

    /// A parameter that must be provided a value
    Parameter(Parameter),
}

#[derive(Debug)]
pub enum TokenMatchError {
    ParameterMatchError { input: SmolStr, msg: SmolStr },
    MissingParameter { name: SmolStr },
}

#[derive(Debug)]
pub(super) struct TokenMatchValue {
    pub raw: SmolStr,
    pub param: Option<(SmolStr, ParameterValue)>,
}

impl TokenMatchValue {
    fn new_match(raw: impl Into<SmolStr>) -> TryMatchResult {
        Some(Ok(Some(Self {
            raw: raw.into(),
            param: None,
        })))
    }

    fn new_match_param(
        raw: impl Into<SmolStr>,
        param_name: impl Into<SmolStr>,
        param: ParameterValue,
    ) -> TryMatchResult {
        Some(Ok(Some(Self {
            raw: raw.into(),
            param: Some((param_name.into(), param)),
        })))
    }
}

/// None -> no match
/// Some(Ok(None)) -> match, no value
/// Some(Ok(Some(_))) -> match, with value
/// Some(Err(_)) -> error while matching
// q: why do this while we could have a NoMatch in TokenMatchError?
// a: because we want to differentiate between no match and match failure (it matched with an error)
//    "no match" has a different charecteristic because we want to continue matching other tokens...
//    ...while "match failure" means we should stop matching and return the error
type TryMatchResult = Option<Result<Option<TokenMatchValue>, TokenMatchError>>;

impl Token {
    pub(super) fn try_match(&self, input: Option<&str>) -> TryMatchResult {
        let input = match input {
            Some(input) => input,
            None => {
                // short circuit on:
                return match self {
                    // empty token
                    Self::Empty => Some(Ok(None)),
                    // missing paramaters
                    Self::Parameter(param) => Some(Err(TokenMatchError::MissingParameter {
                        name: param.name().into(),
                    })),
                    // everything else doesnt match if no input anyway
                    Self::Value { .. } => None,
                    // don't add a _ match here!
                };
            }
        };
        let input = input.trim();

        // try actually matching stuff
        match self {
            Self::Empty => None,
            Self::Value { name, aliases } => (aliases.iter().chain(std::iter::once(name)))
                .any(|v| v.eq(input))
                .then(|| TokenMatchValue::new_match(input))
                .unwrap_or(None),
            Self::Parameter(param) => match param.kind().match_value(input) {
                Ok(matched) => TokenMatchValue::new_match_param(input, param.name(), matched),
                Err(err) => Some(Err(TokenMatchError::ParameterMatchError {
                    input: input.into(),
                    msg: err,
                })),
            }, // don't add a _ match here!
        }
    }
}

impl Display for Token {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Empty => write!(f, ""),
            Self::Value { name, .. } => write!(f, "{name}"),
            Self::Parameter(param) => param.kind().format(f, param.name()),
        }
    }
}

impl<const L: usize> From<(&str, [&str; L])> for Token {
    fn from((name, aliases): (&str, [&str; L])) -> Self {
        Self::Value {
            name: name.into(),
            aliases: aliases.into_iter().map(SmolStr::new).collect::<Vec<_>>(),
        }
    }
}

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

#[derive(Debug, Clone)]
pub struct TokensIterator {
    inner: Vec<Token>,
}

impl Iterator for TokensIterator {
    type Item = Token;

    fn next(&mut self) -> Option<Self::Item> {
        (self.inner.len() > 0).then(|| self.inner.remove(0))
    }
}

impl<T: Into<Token>> From<T> for TokensIterator {
    fn from(value: T) -> Self {
        Self {
            inner: vec![value.into()],
        }
    }
}

impl<const L: usize> From<[Token; L]> for TokensIterator {
    fn from(value: [Token; L]) -> Self {
        Self {
            inner: value.into_iter().collect(),
        }
    }
}

impl<const L: usize> From<[Self; L]> for TokensIterator {
    fn from(value: [Self; L]) -> Self {
        Self {
            inner: value
                .into_iter()
                .map(|t| t.collect::<Vec<_>>())
                .flatten()
                .collect(),
        }
    }
}

impl From<Vec<Token>> for TokensIterator {
    fn from(value: Vec<Token>) -> Self {
        Self { inner: value }
    }
}

#[macro_export]
macro_rules! tokens {
    ($($v:expr),+$(,)*) => {
        $crate::token::TokensIterator::from([$($crate::token::TokensIterator::from($v.clone())),+])
    };
}
