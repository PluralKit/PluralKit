use std::{
    fmt::{Debug, Display},
    hash::Hash,
    ops::Not,
    sync::Arc,
};

use smol_str::{SmolStr, ToSmolStr};

use crate::{parameter::Parameter, Parameter as FfiParam};

pub type ParamName = &'static str;

#[derive(Debug, Clone)]
pub enum Token {
    /// Token used to represent a finished command (i.e. no more parameters required)
    // todo: this is likely not the right way to represent this
    Empty,

    /// multi-token matching
    /// todo: FullString tokens don't work properly in this (they don't get passed the rest of the input)
    Any(Vec<Token>),

    /// A bot-defined command / subcommand (usually) (eg. "member" in `pk;member MyName`)
    Value(Vec<SmolStr>),

    /// A parameter that must be provided a value
    Parameter(ParamName, Arc<dyn Parameter>),
}

#[macro_export]
macro_rules! any {
    ($($t:expr),+) => {
        Token::Any(vec![$(Token::from($t)),+])
    };
}

impl PartialEq for Token {
    fn eq(&self, other: &Self) -> bool {
        match (self, other) {
            (Self::Any(l0), Self::Any(r0)) => l0 == r0,
            (Self::Value(l0), Self::Value(r0)) => l0 == r0,
            (Self::Parameter(l0, _), Self::Parameter(r0, _)) => l0 == r0,
            (Self::Empty, Self::Empty) => true,
            _ => false,
        }
    }
}
impl Eq for Token {}

impl Hash for Token {
    fn hash<H: std::hash::Hasher>(&self, state: &mut H) {
        core::mem::discriminant(self).hash(state);
        match self {
            Token::Empty => {}
            Token::Any(vec) => vec.hash(state),
            Token::Value(vec) => vec.hash(state),
            Token::Parameter(name, _) => name.hash(state),
        }
    }
}

#[derive(Debug)]
pub enum TokenMatchError {
    ParameterMatchError { input: SmolStr, msg: SmolStr },
    MissingParameter { name: ParamName },
    MissingAny { tokens: Vec<Token> },
}

#[derive(Debug)]
pub struct TokenMatchValue {
    pub raw: SmolStr,
    pub param: Option<(ParamName, FfiParam)>,
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
        param_name: ParamName,
        param: FfiParam,
    ) -> TryMatchResult {
        Some(Ok(Some(Self {
            raw: raw.into(),
            param: Some((param_name, param)),
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
    pub fn try_match(&self, input: Option<&str>) -> TryMatchResult {
        let input = match input {
            Some(input) => input,
            None => {
                // short circuit on:
                return match self {
                    // empty token
                    Self::Empty => Some(Ok(None)),
                    // missing paramaters
                    Self::Parameter(name, _) => {
                        Some(Err(TokenMatchError::MissingParameter { name }))
                    }
                    Self::Any(tokens) => tokens.is_empty().then_some(None).unwrap_or_else(|| {
                        Some(Err(TokenMatchError::MissingAny {
                            tokens: tokens.clone(),
                        }))
                    }),
                    // everything else doesnt match if no input anyway
                    Self::Value(_) => None,
                    // don't add a _ match here!
                };
            }
        };
        let input = input.trim();

        // try actually matching stuff
        match self {
            Self::Empty => None,
            Self::Any(tokens) => tokens
                .iter()
                .map(|t| t.try_match(Some(input)))
                .find(|r| !matches!(r, None))
                .unwrap_or(None),
            Self::Value(values) => values
                .iter()
                .any(|v| v.eq(input))
                .then(|| TokenMatchValue::new_match(input))
                .unwrap_or(None),
            Self::Parameter(name, param) => match param.match_value(input) {
                Ok(matched) => TokenMatchValue::new_match_param(input, name, matched),
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
            Token::Empty => write!(f, ""),
            Token::Any(vec) => {
                write!(f, "(")?;
                for (i, token) in vec.iter().enumerate() {
                    if i != 0 {
                        write!(f, "|")?;
                    }
                    write!(f, "{}", token)?;
                }
                write!(f, ")")
            }
            Token::Value(vec) if vec.is_empty().not() => write!(f, "{}", vec.first().unwrap()),
            Token::Value(_) => Ok(()), // if value token has no values (lol), don't print anything
            Token::Parameter(name, param) => param.format(f, name),
        }
    }
}

impl From<&str> for Token {
    fn from(value: &str) -> Self {
        Token::Value(vec![value.to_smolstr()])
    }
}

impl<P: Parameter + 'static> From<P> for Token {
    fn from(value: P) -> Self {
        Token::Parameter(value.default_name(), Arc::new(value))
    }
}

impl<P: Parameter + 'static> From<(ParamName, P)> for Token {
    fn from(value: (ParamName, P)) -> Self {
        Token::Parameter(value.0, Arc::new(value.1))
    }
}

impl<const L: usize, T: Into<Token>> From<[T; L]> for Token {
    fn from(value: [T; L]) -> Self {
        let tokens = value.into_iter().map(|s| s.into()).collect::<Vec<_>>();
        if tokens.iter().all(|t| matches!(t, Token::Value(_))) {
            let values = tokens
                .into_iter()
                .flat_map(|t| match t {
                    Token::Value(v) => v,
                    _ => unreachable!(),
                })
                .collect::<Vec<_>>();
            return Token::Value(values);
        }
        Token::Any(tokens)
    }
}
