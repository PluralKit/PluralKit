use std::{fmt::Display, ops::Not, str::FromStr};

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
    Enable(ParamName),
    Disable(ParamName),
    Toggle(ParamName),

    /// reset, clear, default
    Reset(ParamName),

    // todo: currently not included in command definitions
    // todo: flags with values
    Flag,
}

#[derive(Debug)]
pub enum TokenMatchError {
    MissingParameter { name: ParamName },
}

#[derive(Debug)]
pub struct TokenMatchedValue {
    pub raw: SmolStr,
    pub param: Option<(ParamName, Parameter)>,
}

impl TokenMatchedValue {
    fn new_match(raw: impl Into<SmolStr>) -> TryMatchResult {
        Some(Ok(Some(Self {
            raw: raw.into(),
            param: None,
        })))
    }

    fn new_match_param(
        raw: impl Into<SmolStr>,
        param_name: ParamName,
        param: Parameter,
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
type TryMatchResult = Option<Result<Option<TokenMatchedValue>, TokenMatchError>>;

impl Token {
    pub fn try_match(&self, input: Option<SmolStr>) -> TryMatchResult {
        let input = match input {
            Some(input) => input,
            None => {
                // short circuit on:
                return match self {
                    // empty token
                    Self::Empty => Some(Ok(None)),
                    // missing paramaters
                    Self::FullString(param_name)
                    | Self::MemberRef(param_name)
                    | Self::MemberPrivacyTarget(param_name)
                    | Self::SystemRef(param_name)
                    | Self::PrivacyLevel(param_name)
                    | Self::Toggle(param_name)
                    | Self::Enable(param_name)
                    | Self::Disable(param_name)
                    | Self::Reset(param_name) => {
                        Some(Err(TokenMatchError::MissingParameter { name: param_name }))
                    }
                    Self::Any(tokens) => tokens.is_empty().then_some(None).unwrap_or_else(|| {
                        let mut results = tokens.iter().map(|t| t.try_match(None));
                        results.find(|r| !matches!(r, None)).unwrap_or(None)
                    }),
                    // everything else doesnt match if no input anyway
                    Token::Value(_) => None,
                    Token::Flag => None,
                    // don't add a _ match here!
                };
            }
        };
        let input = input.trim();

        // try actually matching stuff
        match self {
            Self::Empty => None,
            Self::Flag => unreachable!(), // matched upstream (dusk: i don't really like this tbh)
            Self::Any(tokens) => tokens
                .iter()
                .map(|t| t.try_match(Some(input.into())))
                .find(|r| !matches!(r, None))
                .unwrap_or(None),
            Self::Value(values) => values
                .iter()
                .any(|v| v.eq(input))
                .then(|| TokenMatchedValue::new_match(input))
                .unwrap_or(None),
            Self::FullString(param_name) => TokenMatchedValue::new_match_param(
                input,
                param_name,
                Parameter::OpaqueString { raw: input.into() },
            ),
            Self::SystemRef(param_name) => TokenMatchedValue::new_match_param(
                input,
                param_name,
                Parameter::SystemRef {
                    system: input.into(),
                },
            ),
            Self::MemberRef(param_name) => TokenMatchedValue::new_match_param(
                input,
                param_name,
                Parameter::MemberRef {
                    member: input.into(),
                },
            ),
            Self::MemberPrivacyTarget(param_name) => match MemberPrivacyTarget::from_str(input) {
                Ok(target) => TokenMatchedValue::new_match_param(
                    input,
                    param_name,
                    Parameter::MemberPrivacyTarget {
                        target: target.as_ref().into(),
                    },
                ),
                Err(_) => None,
            },
            Self::PrivacyLevel(param_name) => match PrivacyLevel::from_str(input) {
                Ok(level) => TokenMatchedValue::new_match_param(
                    input,
                    param_name,
                    Parameter::PrivacyLevel {
                        level: level.as_ref().into(),
                    },
                ),
                Err(_) => None,
            },
            Self::Toggle(param_name) => match Enable::from_str(input)
                .map(Into::<bool>::into)
                .or_else(|_| Disable::from_str(input).map(Into::<bool>::into))
            {
                Ok(toggle) => TokenMatchedValue::new_match_param(
                    input,
                    param_name,
                    Parameter::Toggle { toggle },
                ),
                Err(_) => None,
            },
            Self::Enable(param_name) => match Enable::from_str(input) {
                Ok(t) => TokenMatchedValue::new_match_param(
                    input,
                    param_name,
                    Parameter::Toggle { toggle: t.into() },
                ),
                Err(_) => None,
            },
            Self::Disable(param_name) => match Disable::from_str(input) {
                Ok(t) => TokenMatchedValue::new_match_param(
                    input,
                    param_name,
                    Parameter::Toggle { toggle: t.into() },
                ),
                Err(_) => None,
            },
            Self::Reset(param_name) => match Reset::from_str(input) {
                Ok(_) => TokenMatchedValue::new_match_param(input, param_name, Parameter::Reset),
                Err(_) => None,
            },
            // don't add a _ match here!
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
                        write!(f, " | ")?;
                    }
                    write!(f, "{}", token)?;
                }
                write!(f, ")")
            }
            Token::Value(vec) if vec.is_empty().not() => write!(f, "{}", vec.first().unwrap()),
            Token::Value(_) => Ok(()), // if value token has no values (lol), don't print anything
            // todo: it might not be the best idea to directly use param name here (what if we want to display something else but keep the name? or translations?)
            Token::FullString(param_name) => write!(f, "[{}]", param_name),
            Token::MemberRef(param_name) => write!(f, "<{}>", param_name),
            Token::SystemRef(param_name) => write!(f, "<{}>", param_name),
            Token::MemberPrivacyTarget(param_name) => write!(f, "[{}]", param_name),
            Token::PrivacyLevel(param_name) => write!(f, "[{}]", param_name),
            Token::Enable(_) => write!(f, "on"),
            Token::Disable(_) => write!(f, "off"),
            Token::Toggle(_) => write!(f, "on/off"),
            Token::Reset(_) => write!(f, "reset"),
            Token::Flag => unreachable!("flag tokens should never be in command definitions"),
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
    Description,
    Banner,
    Avatar,
    Birthday,
    Pronouns,
    Proxy,
    Metadata,
}

impl AsRef<str> for MemberPrivacyTarget {
    fn as_ref(&self) -> &str {
        match self {
            Self::Visibility => "visibility",
            Self::Name => "name",
            Self::Description => "description",
            Self::Banner => "banner",
            Self::Avatar => "avatar",
            Self::Birthday => "birthday",
            Self::Pronouns => "pronouns",
            Self::Proxy => "proxy",
            Self::Metadata => "metadata",
        }
    }
}

impl FromStr for MemberPrivacyTarget {
    // todo: figure out how to represent these errors best
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s.to_lowercase().as_str() {
            "visibility" => Ok(Self::Visibility),
            "name" => Ok(Self::Name),
            "description" => Ok(Self::Description),
            "banner" => Ok(Self::Banner),
            "avatar" => Ok(Self::Avatar),
            "birthday" => Ok(Self::Birthday),
            "pronouns" => Ok(Self::Pronouns),
            "proxy" => Ok(Self::Proxy),
            "metadata" => Ok(Self::Metadata),
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

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct Enable;

impl AsRef<str> for Enable {
    fn as_ref(&self) -> &str {
        "on"
    }
}

impl FromStr for Enable {
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "on" | "yes" | "true" | "enable" | "enabled" => Ok(Self),
            _ => Err(()),
        }
    }
}

impl Into<bool> for Enable {
    fn into(self) -> bool {
        true
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct Disable;

impl AsRef<str> for Disable {
    fn as_ref(&self) -> &str {
        "off"
    }
}

impl FromStr for Disable {
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "off" | "no" | "false" | "disable" | "disabled" => Ok(Self),
            _ => Err(()),
        }
    }
}

impl Into<bool> for Disable {
    fn into(self) -> bool {
        false
    }
}
