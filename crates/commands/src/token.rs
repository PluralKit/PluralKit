use smol_str::{SmolStr, ToSmolStr};

type ParamName = &'static str;

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub enum Token {
    /// Token used to represent a finished command (i.e. no more parameters required)
    // todo: this is likely not the right way to represent this
    Empty,

    /// A bot-defined value ("member" in `pk;member MyName`)
    Value(Vec<SmolStr>),
    /// A command defined by multiple values
    // todo!
    MultiValue(Vec<Vec<SmolStr>>),

    FullString(ParamName),

    /// Member reference (hid or member name)
    MemberRef(ParamName),
    MemberPrivacyTarget(ParamName),

    /// System reference
    SystemRef(ParamName),

    PrivacyLevel(ParamName),

    // currently not included in command definitions
    // todo: flags with values
    Flag,
}

pub enum TokenMatchResult {
    NoMatch,
    /// Token matched, optionally with a value.
    Match(Option<SmolStr>),
    MissingParameter {
        name: ParamName,
    },
}

// move this somewhere else
const MEMBER_PRIVACY_TARGETS: &[&str] = &["visibility", "name", "todo"];

impl Token {
    pub fn try_match(&self, input: Option<SmolStr>) -> TokenMatchResult {
        // short circuit on empty things
        if matches!(self, Self::Empty) && input.is_none() {
            return TokenMatchResult::Match(None);
        } else if input.is_none() {
            return match self {
                Self::FullString(param_name) => TokenMatchResult::MissingParameter { name: param_name },
                Self::MemberRef(param_name) => TokenMatchResult::MissingParameter { name: param_name },
                Self::MemberPrivacyTarget(param_name) => TokenMatchResult::MissingParameter { name: param_name },
                Self::SystemRef(param_name) => TokenMatchResult::MissingParameter { name: param_name },
                Self::PrivacyLevel(param_name) => TokenMatchResult::MissingParameter { name: param_name },
                _ => TokenMatchResult::NoMatch,
            }
        }

        let input = input.as_ref().map(|s| s.trim()).unwrap();

        // try actually matching stuff
        match self {
            Self::Empty => return TokenMatchResult::NoMatch,
            Self::Flag => unreachable!(), // matched upstream
            Self::Value(values) if values.iter().any(|v| v.eq(input)) => {
                return TokenMatchResult::Match(None);
            }
            Self::Value(_) => {}
            Self::MultiValue(_) => todo!(),
            Self::FullString(_) => return TokenMatchResult::Match(Some(input.into())),
            Self::SystemRef(_) => return TokenMatchResult::Match(Some(input.into())),
            Self::MemberRef(_) => return TokenMatchResult::Match(Some(input.into())),
            Self::MemberPrivacyTarget(_) if MEMBER_PRIVACY_TARGETS.contains(&input) => {
                return TokenMatchResult::Match(Some(input.into()))
            }
            Self::MemberPrivacyTarget(_) => {}
            Self::PrivacyLevel(_) if input == "public" || input == "private" => {
                return TokenMatchResult::Match(Some(input.into()))
            }
            Self::PrivacyLevel(_) => {}
        }
        // note: must not add a _ case to the above match
        // instead, for conditional matches, also add generic cases with no return

        return TokenMatchResult::NoMatch;
    }
}

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
