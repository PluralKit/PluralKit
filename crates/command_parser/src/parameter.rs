use std::{
    fmt::{Debug, Display},
    str::FromStr,
};

use smol_str::SmolStr;

use crate::token::{Token, TokenMatchResult};

#[derive(Debug, Clone)]
pub enum ParameterValue {
    OpaqueString(String),
    MemberRef(String),
    SystemRef(String),
    MemberPrivacyTarget(String),
    PrivacyLevel(String),
    Toggle(bool),
}

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct Parameter {
    name: SmolStr,
    kind: ParameterKind,
}

impl Parameter {
    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn kind(&self) -> ParameterKind {
        self.kind
    }
}

impl Display for Parameter {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self.kind {
            ParameterKind::OpaqueString | ParameterKind::OpaqueStringRemainder => {
                write!(f, "[{}]", self.name)
            }
            ParameterKind::MemberRef => write!(f, "<target member>"),
            ParameterKind::SystemRef => write!(f, "<target system>"),
            ParameterKind::MemberPrivacyTarget => write!(f, "<privacy target>"),
            ParameterKind::PrivacyLevel => write!(f, "[privacy level]"),
            ParameterKind::Toggle => write!(f, "on/off"),
        }
    }
}

impl From<ParameterKind> for Parameter {
    fn from(value: ParameterKind) -> Self {
        Parameter {
            name: value.default_name().into(),
            kind: value,
        }
    }
}

impl From<(&str, ParameterKind)> for Parameter {
    fn from((name, kind): (&str, ParameterKind)) -> Self {
        Parameter {
            name: name.into(),
            kind,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum ParameterKind {
    OpaqueString,
    OpaqueStringRemainder,
    MemberRef,
    SystemRef,
    MemberPrivacyTarget,
    PrivacyLevel,
    Toggle,
}

impl ParameterKind {
    pub(crate) fn default_name(&self) -> &str {
        match self {
            ParameterKind::OpaqueString => "string",
            ParameterKind::OpaqueStringRemainder => "string",
            ParameterKind::MemberRef => "target",
            ParameterKind::SystemRef => "target",
            ParameterKind::MemberPrivacyTarget => "member_privacy_target",
            ParameterKind::PrivacyLevel => "privacy_level",
            ParameterKind::Toggle => "toggle",
        }
    }

    pub(crate) fn remainder(&self) -> bool {
        matches!(self, ParameterKind::OpaqueStringRemainder)
    }

    pub(crate) fn match_value(&self, input: &str) -> Result<ParameterValue, SmolStr> {
        match self {
            ParameterKind::OpaqueString | ParameterKind::OpaqueStringRemainder => {
                Ok(ParameterValue::OpaqueString(input.into()))
            }
            ParameterKind::MemberRef => Ok(ParameterValue::MemberRef(input.into())),
            ParameterKind::SystemRef => Ok(ParameterValue::SystemRef(input.into())),
            ParameterKind::MemberPrivacyTarget => MemberPrivacyTargetKind::from_str(input)
                .map(|target| ParameterValue::MemberPrivacyTarget(target.as_ref().into())),
            ParameterKind::PrivacyLevel => PrivacyLevelKind::from_str(input)
                .map(|level| ParameterValue::PrivacyLevel(level.as_ref().into())),
            ParameterKind::Toggle => {
                Toggle::from_str(input).map(|t| ParameterValue::Toggle(t.into()))
            }
        }
    }
}

pub enum MemberPrivacyTargetKind {
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

impl AsRef<str> for MemberPrivacyTargetKind {
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

impl FromStr for MemberPrivacyTargetKind {
    // todo: figure out how to represent these errors best
    type Err = SmolStr;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        // todo: this doesnt parse all the possible ways
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
            _ => Err("invalid member privacy target".into()),
        }
    }
}

pub enum PrivacyLevelKind {
    Public,
    Private,
}

impl AsRef<str> for PrivacyLevelKind {
    fn as_ref(&self) -> &str {
        match self {
            Self::Public => "public",
            Self::Private => "private",
        }
    }
}

impl FromStr for PrivacyLevelKind {
    type Err = SmolStr; // todo

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "public" => Ok(PrivacyLevelKind::Public),
            "private" => Ok(PrivacyLevelKind::Private),
            _ => Err("invalid privacy level".into()),
        }
    }
}

#[derive(Debug, Clone, Copy, Eq, Hash, PartialEq)]
pub enum Toggle {
    On,
    Off,
}

impl FromStr for Toggle {
    type Err = SmolStr;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        let matches_self = |toggle: &Self| {
            matches!(
                Token::from(*toggle).try_match(Some(s)),
                Some(TokenMatchResult::MatchedValue)
            )
        };
        [Self::On, Self::Off]
            .into_iter()
            .find(matches_self)
            .ok_or_else(|| SmolStr::new("invalid toggle, must be on/off"))
    }
}

impl From<Toggle> for Token {
    fn from(toggle: Toggle) -> Self {
        match toggle {
            Toggle::On => Self::from(("on", ["yes", "true", "enable", "enabled"])),
            Toggle::Off => Self::from(("off", ["no", "false", "disable", "disabled"])),
        }
    }
}

impl Into<bool> for Toggle {
    fn into(self) -> bool {
        match self {
            Toggle::On => true,
            Toggle::Off => false,
        }
    }
}
