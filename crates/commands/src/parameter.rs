use std::{fmt::Debug, str::FromStr};

use smol_str::SmolStr;

use crate::{ParamName, Parameter as FfiParam};

pub trait Parameter: Debug + Send + Sync {
    fn remainder(&self) -> bool {
        false
    }
    fn default_name(&self) -> ParamName;
    fn format(&self, f: &mut std::fmt::Formatter, name: &str) -> std::fmt::Result;
    fn match_value(&self, input: &str) -> Result<FfiParam, SmolStr>;
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct OpaqueString(bool);

impl OpaqueString {
    pub const SINGLE: Self = Self(false);
    pub const REMAINDER: Self = Self(true);
}

impl Parameter for OpaqueString {
    fn remainder(&self) -> bool {
        self.0
    }

    fn default_name(&self) -> ParamName {
        "string"
    }

    fn format(&self, f: &mut std::fmt::Formatter, name: &str) -> std::fmt::Result {
        write!(f, "[{name}]")
    }

    fn match_value(&self, input: &str) -> Result<FfiParam, SmolStr> {
        Ok(FfiParam::OpaqueString { raw: input.into() })
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct MemberRef;

impl Parameter for MemberRef {
    fn default_name(&self) -> ParamName {
        "member"
    }

    fn format(&self, f: &mut std::fmt::Formatter, _: &str) -> std::fmt::Result {
        write!(f, "<target member>")
    }

    fn match_value(&self, input: &str) -> Result<FfiParam, SmolStr> {
        Ok(FfiParam::MemberRef {
            member: input.into(),
        })
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct SystemRef;

impl Parameter for SystemRef {
    fn default_name(&self) -> ParamName {
        "system"
    }

    fn format(&self, f: &mut std::fmt::Formatter, _: &str) -> std::fmt::Result {
        write!(f, "<target system>")
    }

    fn match_value(&self, input: &str) -> Result<FfiParam, SmolStr> {
        Ok(FfiParam::SystemRef {
            system: input.into(),
        })
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct MemberPrivacyTarget;

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

impl Parameter for MemberPrivacyTarget {
    fn default_name(&self) -> ParamName {
        "member_privacy_target"
    }

    fn format(&self, f: &mut std::fmt::Formatter, _: &str) -> std::fmt::Result {
        write!(f, "<privacy target>")
    }

    fn match_value(&self, input: &str) -> Result<FfiParam, SmolStr> {
        MemberPrivacyTargetKind::from_str(input).map(|target| FfiParam::MemberPrivacyTarget {
            target: target.as_ref().into(),
        })
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct PrivacyLevel;

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

impl Parameter for PrivacyLevel {
    fn default_name(&self) -> ParamName {
        "privacy_level"
    }

    fn format(&self, f: &mut std::fmt::Formatter, _: &str) -> std::fmt::Result {
        write!(f, "[privacy level]")
    }

    fn match_value(&self, input: &str) -> Result<FfiParam, SmolStr> {
        PrivacyLevelKind::from_str(input).map(|level| FfiParam::PrivacyLevel {
            level: level.as_ref().into(),
        })
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
    type Err = SmolStr;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "reset" | "clear" | "default" => Ok(Self),
            _ => Err("not reset".into()),
        }
    }
}

impl Parameter for Reset {
    fn default_name(&self) -> ParamName {
        "reset"
    }

    fn format(&self, f: &mut std::fmt::Formatter, _: &str) -> std::fmt::Result {
        write!(f, "reset")
    }

    fn match_value(&self, input: &str) -> Result<FfiParam, SmolStr> {
        Self::from_str(input).map(|_| FfiParam::Toggle { toggle: true })
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct Toggle;

impl Parameter for Toggle {
    fn default_name(&self) -> ParamName {
        "toggle"
    }

    fn format(&self, f: &mut std::fmt::Formatter, _: &str) -> std::fmt::Result {
        write!(f, "on/off")
    }

    fn match_value(&self, input: &str) -> Result<FfiParam, SmolStr> {
        Enable::from_str(input)
            .map(Into::<bool>::into)
            .or_else(|_| Disable::from_str(input).map(Into::<bool>::into))
            .map(|toggle| FfiParam::Toggle { toggle })
            .map_err(|_| "invalid toggle".into())
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct Enable;

impl FromStr for Enable {
    type Err = SmolStr;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "on" | "yes" | "true" | "enable" | "enabled" => Ok(Self),
            _ => Err("invalid enable".into()),
        }
    }
}

impl Parameter for Enable {
    fn default_name(&self) -> ParamName {
        "enable"
    }

    fn format(&self, f: &mut std::fmt::Formatter, _: &str) -> std::fmt::Result {
        write!(f, "on")
    }

    fn match_value(&self, input: &str) -> Result<FfiParam, SmolStr> {
        Self::from_str(input).map(|e| FfiParam::Toggle { toggle: e.into() })
    }
}

impl Into<bool> for Enable {
    fn into(self) -> bool {
        true
    }
}

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub struct Disable;

impl FromStr for Disable {
    type Err = SmolStr;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "off" | "no" | "false" | "disable" | "disabled" => Ok(Self),
            _ => Err("invalid disable".into()),
        }
    }
}

impl Into<bool> for Disable {
    fn into(self) -> bool {
        false
    }
}

impl Parameter for Disable {
    fn default_name(&self) -> ParamName {
        "disable"
    }

    fn format(&self, f: &mut std::fmt::Formatter, _: &str) -> std::fmt::Result {
        write!(f, "off")
    }

    fn match_value(&self, input: &str) -> Result<FfiParam, SmolStr> {
        Self::from_str(input).map(|e| FfiParam::Toggle { toggle: e.into() })
    }
}
