use std::{
    fmt::{Debug, Display},
    str::FromStr,
};

use regex::Regex;
use smol_str::{SmolStr, format_smolstr};

use crate::token::{Token, TokenMatchResult};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum ParameterKind {
    OpaqueString,
    OpaqueInt,
    MemberRef,
    MemberRefs,
    GroupRef,
    GroupRefs,
    SystemRef,
    UserRef,
    MessageRef,
    ChannelRef,
    GuildRef,
    MemberPrivacyTarget,
    GroupPrivacyTarget,
    SystemPrivacyTarget,
    PrivacyLevel,
    Toggle,
    Avatar,
    ProxySwitchAction,
}

impl ParameterKind {
    pub(crate) fn default_name(&self) -> &str {
        match self {
            ParameterKind::OpaqueString => "string",
            ParameterKind::OpaqueInt => "number",
            ParameterKind::MemberRef => "target",
            ParameterKind::MemberRefs => "targets",
            ParameterKind::GroupRef => "target",
            ParameterKind::GroupRefs => "targets",
            ParameterKind::SystemRef => "target",
            ParameterKind::UserRef => "target",
            ParameterKind::MessageRef => "target",
            ParameterKind::ChannelRef => "target",
            ParameterKind::GuildRef => "target",
            ParameterKind::MemberPrivacyTarget => "member_privacy_target",
            ParameterKind::GroupPrivacyTarget => "group_privacy_target",
            ParameterKind::SystemPrivacyTarget => "system_privacy_target",
            ParameterKind::PrivacyLevel => "privacy_level",
            ParameterKind::Toggle => "toggle",
            ParameterKind::Avatar => "avatar",
            ParameterKind::ProxySwitchAction => "proxy_switch_action",
        }
    }
}

#[derive(Debug, Clone)]
pub enum ParameterValue {
    OpaqueString(String),
    OpaqueInt(i32),
    MemberRef(String),
    MemberRefs(Vec<String>),
    GroupRef(String),
    GroupRefs(Vec<String>),
    SystemRef(String),
    UserRef(u64),
    MessageRef(Option<u64>, Option<u64>, u64),
    ChannelRef(u64),
    GuildRef(u64),
    MemberPrivacyTarget(String),
    GroupPrivacyTarget(String),
    SystemPrivacyTarget(String),
    PrivacyLevel(String),
    Toggle(bool),
    Avatar(String),
    ProxySwitchAction(ProxySwitchAction),
    Null,
}

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct Parameter {
    name: SmolStr,
    kind: ParameterKind,
    remainder: bool,
    optional: bool,
    skip: bool,
}

impl Parameter {
    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn kind(&self) -> ParameterKind {
        self.kind
    }

    pub fn remainder(mut self) -> Self {
        self.remainder = true;
        self
    }

    pub fn optional(mut self) -> Self {
        self.optional = true;
        self
    }

    pub fn skip(mut self) -> Self {
        self.skip = true;
        self
    }

    pub fn is_remainder(&self) -> bool {
        self.remainder
    }

    pub fn is_optional(&self) -> bool {
        self.optional
    }

    pub fn is_skip(&self) -> bool {
        self.skip
    }

    pub fn match_value(&self, input: &str) -> Result<ParameterValue, SmolStr> {
        match self.kind {
            // TODO: actually parse image url
            ParameterKind::OpaqueString => Ok(ParameterValue::OpaqueString(input.into())),
            ParameterKind::OpaqueInt => input
                .parse::<i32>()
                .map(|num| ParameterValue::OpaqueInt(num))
                .map_err(|err| format_smolstr!("invalid integer: {err}")),
            ParameterKind::GroupRef => Ok(ParameterValue::GroupRef(input.into())),
            ParameterKind::GroupRefs => Ok(ParameterValue::GroupRefs(
                input.split(' ').map(|s| s.trim().to_string()).collect(),
            )),
            ParameterKind::MemberRef => Ok(ParameterValue::MemberRef(input.into())),
            ParameterKind::MemberRefs => Ok(ParameterValue::MemberRefs(
                input.split(' ').map(|s| s.trim().to_string()).collect(),
            )),
            ParameterKind::SystemRef => Ok(ParameterValue::SystemRef(input.into())),
            ParameterKind::UserRef => {
                if let Ok(user_id) = input.parse::<u64>() {
                    return Ok(ParameterValue::UserRef(user_id));
                }

                static RE: std::sync::LazyLock<Regex> =
                    std::sync::LazyLock::new(|| Regex::new(r"<@!?(\\d{17,19})>").unwrap());
                if let Some(captures) = RE.captures(&input) {
                    return captures[1]
                        .parse::<u64>()
                        .map(|id| ParameterValue::UserRef(id))
                        .map_err(|_| SmolStr::new("invalid user ID"));
                }

                Err(SmolStr::new("invalid user ID"))
            }
            ParameterKind::MemberPrivacyTarget => MemberPrivacyTargetKind::from_str(input)
                .map(|target| ParameterValue::MemberPrivacyTarget(target.as_ref().into())),
            ParameterKind::GroupPrivacyTarget => GroupPrivacyTargetKind::from_str(input)
                .map(|target| ParameterValue::GroupPrivacyTarget(target.as_ref().into())),
            ParameterKind::SystemPrivacyTarget => SystemPrivacyTargetKind::from_str(input)
                .map(|target| ParameterValue::SystemPrivacyTarget(target.as_ref().into())),
            ParameterKind::PrivacyLevel => PrivacyLevelKind::from_str(input)
                .map(|level| ParameterValue::PrivacyLevel(level.as_ref().into())),
            ParameterKind::Toggle => {
                Toggle::from_str(input).map(|t| ParameterValue::Toggle(t.into()))
            }
            ParameterKind::Avatar => Ok(ParameterValue::Avatar(input.into())),
            ParameterKind::MessageRef => {
                if let Ok(message_id) = input.parse::<u64>() {
                    return Ok(ParameterValue::MessageRef(None, None, message_id));
                }

                static SERVER_RE: std::sync::LazyLock<regex::Regex> = std::sync::LazyLock::new(
                    || {
                        regex::Regex::new(
                        r"https://(?:\w+\.)?discord(?:app)?\.com/channels/(?P<guild>\d+)/(?P<channel>\d+)/(?P<message>\d+)",
                    )
                    .unwrap()
                    },
                );

                static DM_RE: std::sync::LazyLock<regex::Regex> = std::sync::LazyLock::new(|| {
                    regex::Regex::new(
                        r"https://(?:\w+\.)?discord(?:app)?\.com/channels/@me/(?P<channel>\d+)/(?P<message>\d+)",
                    )
                    .unwrap()
                });

                if let Some(captures) = SERVER_RE.captures(input) {
                    let guild_id = captures.parse_id("guild")?;
                    let channel_id = captures.parse_id("channel")?;
                    let message_id = captures.parse_id("message")?;

                    Ok(ParameterValue::MessageRef(
                        Some(guild_id),
                        Some(channel_id),
                        message_id,
                    ))
                } else if let Some(captures) = DM_RE.captures(input) {
                    let channel_id = captures.parse_id("channel")?;
                    let message_id = captures.parse_id("message")?;

                    Ok(ParameterValue::MessageRef(
                        None,
                        Some(channel_id),
                        message_id,
                    ))
                } else {
                    Err(SmolStr::new("invalid message reference"))
                }
            }
            ParameterKind::ChannelRef => {
                let mut text = input;

                if text.len() > 3 && text.starts_with("<#") && text.ends_with('>') {
                    text = &text[2..text.len() - 1];
                }

                text.parse::<u64>()
                    .map(ParameterValue::ChannelRef)
                    .map_err(|_| SmolStr::new("invalid channel ID"))
            }
            ParameterKind::GuildRef => input
                .parse::<u64>()
                .map(ParameterValue::GuildRef)
                .map_err(|_| SmolStr::new("invalid guild ID")),
            ParameterKind::ProxySwitchAction => ProxySwitchAction::from_str(input)
                .map(ParameterValue::ProxySwitchAction)
                .map_err(|_| SmolStr::new("invalid proxy switch action, must be new/add/off")),
        }
    }
}

impl Display for Parameter {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self.kind {
            ParameterKind::OpaqueString => {
                write!(f, "[{}]", self.name)
            }
            ParameterKind::OpaqueInt => {
                write!(f, "[{}]", self.name)
            }
            ParameterKind::MemberRef => write!(f, "<target member>"),
            ParameterKind::MemberRefs => write!(f, "<member 1> <member 2> <member 3>"),
            ParameterKind::GroupRef => write!(f, "<target group>"),
            ParameterKind::GroupRefs => write!(f, "<group 1> <group 2> <group 3>"),
            ParameterKind::SystemRef => write!(f, "<target system>"),
            ParameterKind::UserRef => write!(f, "<target user>"),
            ParameterKind::MessageRef => write!(f, "<target message>"),
            ParameterKind::ChannelRef => write!(f, "<target channel>"),
            ParameterKind::GuildRef => write!(f, "<target guild>"),
            ParameterKind::MemberPrivacyTarget => write!(f, "<privacy target>"),
            ParameterKind::GroupPrivacyTarget => write!(f, "<privacy target>"),
            ParameterKind::SystemPrivacyTarget => write!(f, "<privacy target>"),
            ParameterKind::PrivacyLevel => write!(f, "[privacy level]"),
            ParameterKind::Toggle => write!(f, "<on|off>"),
            ParameterKind::Avatar => write!(f, "<url|@mention>"),
            ParameterKind::ProxySwitchAction => write!(f, "<new|add|off>"),
        }?;
        if self.is_remainder() {
            write!(f, "...")?;
        }
        Ok(())
    }
}

fn is_remainder(kind: ParameterKind) -> bool {
    matches!(kind, ParameterKind::MemberRefs | ParameterKind::GroupRefs)
}

impl From<ParameterKind> for Parameter {
    fn from(value: ParameterKind) -> Self {
        Parameter {
            name: value.default_name().into(),
            kind: value,
            remainder: is_remainder(value),
            optional: false,
            skip: false,
        }
    }
}

impl From<(&str, ParameterKind)> for Parameter {
    fn from((name, kind): (&str, ParameterKind)) -> Self {
        Parameter {
            name: name.into(),
            kind,
            remainder: is_remainder(kind),
            optional: false,
            skip: false,
        }
    }
}

/// if no input is left to parse, this parameter matches to Null
#[derive(Clone)]
pub struct Optional<P: Into<Parameter>>(pub P);

impl<P: Into<Parameter>> From<Optional<P>> for Parameter {
    fn from(value: Optional<P>) -> Self {
        let p = value.0.into();
        p.optional()
    }
}

/// tells the parser to use the remainder of the input as the input to this parameter
#[derive(Clone)]
pub struct Remainder<P: Into<Parameter>>(pub P);

impl<P: Into<Parameter>> From<Remainder<P>> for Parameter {
    fn from(value: Remainder<P>) -> Self {
        let p = value.0.into();
        p.remainder()
    }
}

/// skips the branch this parameter is in if it does not match
#[derive(Clone)]
pub struct Skip<P: Into<Parameter>>(pub P);

impl<P: Into<Parameter>> From<Skip<P>> for Parameter {
    fn from(value: Skip<P>) -> Self {
        let p = value.0.into();
        p.skip()
    }
}

macro_rules! impl_enum {
    ($name:ident ($pretty_name:expr): $($variant:ident),*) => {
        #[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
        pub enum $name {
            $($variant),*
        }

        impl $name {
            pub const PRETTY_NAME: &'static str = $pretty_name;

            pub fn variants() -> impl Iterator<Item = Self> {
                [$(Self::$variant),*].into_iter()
            }

            pub fn variants_str() -> impl Iterator<Item = &'static str> {
                [$(Self::$variant.as_ref()),*].into_iter()
            }

            pub fn get_error() -> SmolStr {
                let pretty_name = Self::PRETTY_NAME;
                let vars = Self::variants_str().intersperse("/").collect::<SmolStr>();
                format_smolstr!("invalid {pretty_name}, must be one of {vars}")
            }
        }
    };
}

impl_enum! {
    MemberPrivacyTargetKind("member privacy target"):
        Visibility,
        Name,
        Description,
        Banner,
        Avatar,
        Birthday,
        Pronouns,
        Proxy,
        Metadata
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
            _ => Err(Self::get_error()),
        }
    }
}

impl_enum! {
    GroupPrivacyTargetKind("group privacy target"):
        Name,
        Icon,
        Description,
        Banner,
        List,
        Metadata,
        Visibility
}

impl AsRef<str> for GroupPrivacyTargetKind {
    fn as_ref(&self) -> &str {
        match self {
            Self::Name => "name",
            Self::Icon => "icon",
            Self::Description => "description",
            Self::Banner => "banner",
            Self::List => "list",
            Self::Metadata => "metadata",
            Self::Visibility => "visibility",
        }
    }
}

impl FromStr for GroupPrivacyTargetKind {
    type Err = SmolStr;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        // todo: this doesnt parse all the possible ways
        match s.to_lowercase().as_str() {
            "name" => Ok(Self::Name),
            "avatar" | "icon" => Ok(Self::Icon),
            "description" => Ok(Self::Description),
            "banner" => Ok(Self::Banner),
            "list" => Ok(Self::List),
            "metadata" => Ok(Self::Metadata),
            "visibility" => Ok(Self::Visibility),
            _ => Err(Self::get_error()),
        }
    }
}

impl_enum! {
    SystemPrivacyTargetKind("system privacy target"):
        Name,
        Avatar,
        Description,
        Banner,
        Pronouns,
        MemberList,
        GroupList,
        Front,
        FrontHistory
}

impl AsRef<str> for SystemPrivacyTargetKind {
    fn as_ref(&self) -> &str {
        match self {
            Self::Name => "name",
            Self::Avatar => "avatar",
            Self::Description => "description",
            Self::Banner => "banner",
            Self::Pronouns => "pronouns",
            Self::MemberList => "members",
            Self::GroupList => "groups",
            Self::Front => "front",
            Self::FrontHistory => "fronthistory",
        }
    }
}

impl FromStr for SystemPrivacyTargetKind {
    type Err = SmolStr;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s.to_lowercase().as_str() {
            "name" => Ok(Self::Name),
            "avatar" | "pfp" | "pic" | "icon" => Ok(Self::Avatar),
            "description" | "desc" | "bio" | "info" => Ok(Self::Description),
            "banner" | "splash" | "cover" => Ok(Self::Banner),
            "pronouns" | "prns" | "pn" => Ok(Self::Pronouns),
            "members" | "memberlist" | "list" => Ok(Self::MemberList),
            "groups" | "gs" => Ok(Self::GroupList),
            "front" | "fronter" | "fronters" => Ok(Self::Front),
            "fronthistory" | "fh" | "switches" => Ok(Self::FrontHistory),
            _ => Err(Self::get_error()),
        }
    }
}

impl_enum!(PrivacyLevelKind("privacy level"): Public, Private);

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
            _ => Err(Self::get_error()),
        }
    }
}

impl_enum!(Toggle("toggle"): On, Off);

impl AsRef<str> for Toggle {
    fn as_ref(&self) -> &str {
        match self {
            Self::On => "on",
            Self::Off => "off",
        }
    }
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
        Self::variants()
            .find(matches_self)
            .ok_or_else(Self::get_error)
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

impl_enum!(ProxySwitchAction("proxy switch action"): New, Add, Off);

impl AsRef<str> for ProxySwitchAction {
    fn as_ref(&self) -> &str {
        match self {
            ProxySwitchAction::New => "new",
            ProxySwitchAction::Add => "add",
            ProxySwitchAction::Off => "off",
        }
    }
}

impl FromStr for ProxySwitchAction {
    type Err = SmolStr;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        Self::variants()
            .find(|action| action.as_ref() == s)
            .ok_or_else(Self::get_error)
    }
}

trait ParseMessageLink {
    fn parse_id(&self, name: &str) -> Result<u64, SmolStr>;
}

impl ParseMessageLink for regex::Captures<'_> {
    fn parse_id(&self, name: &str) -> Result<u64, SmolStr> {
        self.name(name)
            .and_then(|m| m.as_str().parse::<u64>().ok())
            .ok_or_else(|| SmolStr::new(format!("invalid {} ID in message link", name)))
    }
}
