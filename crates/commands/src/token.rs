use std::str::FromStr;

use smol_str::{SmolStr, ToSmolStr};

use crate::Parameter;

type ParamName = &'static str;

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub enum Token {
    /// Token used to represent a finished command (i.e. no more parameters required)
    // todo: this is likely not the right way to represent this
    Empty,

    /// multi-token matching
    Any(Vec<Token>),

    /// A bot-defined command / subcommand (usually) (eg. "member" in `pk;member MyName`)
    Value(Vec<SmolStr>),

    /// Opaque string (eg. "name" in `pk;member new name`)
    FullString(ParamName),

    /// Member reference (hid or member name)
    MemberRef(ParamName),
    /// todo: doc
    MemberPrivacyTarget(ParamName),

    /// System reference
    SystemRef(ParamName),

    /// todo: doc
    PrivacyLevel(ParamName),

    /// on, off; yes, no; true, false
    Toggle(ParamName),

    /// reset, clear, default
    Reset(ParamName),

    // todo: currently not included in command definitions
    // todo: flags with values
    Flag,
}

// #[macro_export]
// macro_rules! any {
//     ($($token:expr),+) => {
//         Token::Any(vec![$($token.to_token()),+])
//     };
// }

#[derive(Debug)]
pub enum TokenMatchResult {
    /// Token did not match.
    NoMatch,
    /// Token matched, optionally with a value.
    Match(Option<TokenMatchedValue>),
    /// A required parameter was missing.
    MissingParameter { name: ParamName },
}

#[derive(Debug)]
pub struct TokenMatchedValue {
    pub raw: SmolStr,
    pub param: Option<(ParamName, Parameter)>,
}

impl TokenMatchResult {
    fn new_match(raw: impl Into<SmolStr>) -> Self {
        Self::Match(Some(TokenMatchedValue {
            raw: raw.into(),
            param: None,
        }))
    }

    fn new_match_param(raw: impl Into<SmolStr>, param_name: ParamName, param: Parameter) -> Self {
        Self::Match(Some(TokenMatchedValue {
            raw: raw.into(),
            param: Some((param_name, param)),
        }))
    }
}

impl Token {
    pub fn try_match(&self, input: Option<SmolStr>) -> TokenMatchResult {
        use TokenMatchResult::*;

        let input = match input {
            Some(input) => input,
            None => {
                // short circuit on:
                return match self {
                    // empty token
                    Self::Empty => Match(None),
                    // missing paramaters
                    Self::FullString(param_name)
                    | Self::MemberRef(param_name)
                    | Self::MemberPrivacyTarget(param_name)
                    | Self::SystemRef(param_name)
                    | Self::PrivacyLevel(param_name)
                    | Self::Toggle(param_name)
                    | Self::Reset(param_name) => MissingParameter { name: param_name },
                    Self::Any(tokens) => tokens.is_empty().then_some(NoMatch).unwrap_or_else(|| {
                        let mut results = tokens.iter().map(|t| t.try_match(None));
                        results.find(|r| !matches!(r, NoMatch)).unwrap_or(NoMatch)
                    }),
                    // everything else doesnt match if no input anyway
                    Token::Value(_) => NoMatch,
                    Token::Flag => NoMatch,
                    // don't add a _ match here!
                };
            }
        };
        let input = input.trim();

        // try actually matching stuff
        match self {
            Self::Empty => NoMatch,
            Self::Flag => unreachable!(), // matched upstream (dusk: i don't really like this tbh)
            Self::Any(tokens) => tokens
                .iter()
                .map(|t| t.try_match(Some(input.into())))
                .find(|r| !matches!(r, NoMatch))
                .unwrap_or(NoMatch),
            Self::Value(values) => values
                .iter()
                .any(|v| v.eq(input))
                .then(|| TokenMatchResult::new_match(input))
                .unwrap_or(NoMatch),
            Self::FullString(param_name) => TokenMatchResult::new_match_param(
                input,
                param_name,
                Parameter::OpaqueString { raw: input.into() },
            ),
            Self::SystemRef(param_name) => TokenMatchResult::new_match_param(
                input,
                param_name,
                Parameter::SystemRef {
                    system: input.into(),
                },
            ),
            Self::MemberRef(param_name) => TokenMatchResult::new_match_param(
                input,
                param_name,
                Parameter::MemberRef {
                    member: input.into(),
                },
            ),
            Self::MemberPrivacyTarget(param_name) => match MemberPrivacyTarget::from_str(input) {
                Ok(target) => TokenMatchResult::new_match_param(
                    input,
                    param_name,
                    Parameter::MemberPrivacyTarget {
                        target: target.as_ref().into(),
                    },
                ),
                Err(_) => NoMatch,
            },
            Self::PrivacyLevel(param_name) => match PrivacyLevel::from_str(input) {
                Ok(level) => TokenMatchResult::new_match_param(
                    input,
                    param_name,
                    Parameter::PrivacyLevel {
                        level: level.as_ref().into(),
                    },
                ),
                Err(_) => NoMatch,
            },

            Self::Toggle(param_name) => match Toggle::from_str(input) {
                Ok(t) => TokenMatchResult::new_match_param(
                    input,
                    param_name,
                    Parameter::Toggle { toggle: t.0 },
                ),
                Err(_) => NoMatch,
            },
            Self::Reset(param_name) => match Reset::from_str(input) {
                Ok(_) => TokenMatchResult::new_match_param(input, param_name, Parameter::Reset),
                Err(_) => NoMatch,
            },
            // don't add a _ match here!
        }
    }
}

/// Convenience trait to convert types into [`Token`]s.
pub trait ToToken {
    fn to_token(&self) -> Token;
}

impl ToToken for Token {
    fn to_token(&self) -> Token {
        self.clone()
    }
}

impl ToToken for &str {
    fn to_token(&self) -> Token {
        Token::Value(vec![self.to_smolstr()])
    }
}

impl ToToken for [&str] {
    fn to_token(&self) -> Token {
        Token::Value(self.into_iter().map(|s| s.to_smolstr()).collect())
    }
}

impl ToToken for [Token] {
    fn to_token(&self) -> Token {
        Token::Any(self.into_iter().map(|s| s.clone()).collect())
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub enum MemberPrivacyTarget {
    Visibility,
    Name,
    // todo
}

impl AsRef<str> for MemberPrivacyTarget {
    fn as_ref(&self) -> &str {
        match self {
            Self::Visibility => "visibility",
            Self::Name => "name",
        }
    }
}

impl FromStr for MemberPrivacyTarget {
    // todo: figure out how to represent these errors best
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "visibility" => Ok(Self::Visibility),
            "name" => Ok(Self::Name),
            _ => Err(()),
        }
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub enum PrivacyLevel {
    Public,
    Private,
}

impl AsRef<str> for PrivacyLevel {
    fn as_ref(&self) -> &str {
        match self {
            Self::Public => "public",
            Self::Private => "private",
        }
    }
}

impl FromStr for PrivacyLevel {
    type Err = (); // todo

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "public" => Ok(Self::Public),
            "private" => Ok(Self::Private),
            _ => Err(()),
        }
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct Toggle(bool);

impl AsRef<str> for Toggle {
    fn as_ref(&self) -> &str {
        // on / off better than others for docs and stuff?
        self.0.then_some("on").unwrap_or("off")
    }
}

impl FromStr for Toggle {
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "on" | "yes" | "true" | "enable" | "enabled" => Ok(Self(true)),
            "off" | "no" | "false" | "disable" | "disabled" => Ok(Self(false)),
            _ => Err(()),
        }
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct Reset;

impl AsRef<str> for Reset {
    fn as_ref(&self) -> &str {
        "reset"
    }
}

impl FromStr for Reset {
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "reset" | "clear" | "default" => Ok(Self),
            _ => Err(()),
        }
    }
}
