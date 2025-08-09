use pk_macros::pk_model;

use sqlx::{postgres::PgTypeInfo, Database, Decode, Postgres, Type};
use std::error::Error;

use crate::{SystemId, _util::fake_enum_impls};

pub const DEFAULT_MEMBER_LIMIT: i32 = 1000;
pub const DEFAULT_GROUP_LIMIT: i32 = 250;

#[derive(serde::Serialize, Debug, Clone)]
#[serde(rename_all = "snake_case")]
pub enum HidPadFormat {
    #[serde(rename = "off")]
    None,
    Left,
    Right,
}
fake_enum_impls!(HidPadFormat);

impl From<i32> for HidPadFormat {
    fn from(value: i32) -> Self {
        match value {
            0 => HidPadFormat::None,
            1 => HidPadFormat::Left,
            2 => HidPadFormat::Right,
            _ => unreachable!(),
        }
    }
}

#[derive(serde::Serialize, Debug, Clone)]
#[serde(rename_all = "snake_case")]
pub enum ProxySwitchAction {
    Off,
    New,
    Add,
}
fake_enum_impls!(ProxySwitchAction);

impl From<i32> for ProxySwitchAction {
    fn from(value: i32) -> Self {
        match value {
            0 => ProxySwitchAction::Off,
            1 => ProxySwitchAction::New,
            2 => ProxySwitchAction::Add,
            _ => unreachable!(),
        }
    }
}

#[pk_model]
struct SystemConfig {
    system: SystemId,
    #[json = "timezone"]
    ui_tz: String,
    #[json = "pings_enabled"]
    pings_enabled: bool,
    #[json = "latch_timeout"]
    latch_timeout: Option<i32>,
    #[json = "member_default_private"]
    member_default_private: bool,
    #[json = "group_default_private"]
    group_default_private: bool,
    #[json = "show_private_info"]
    show_private_info: bool,
    #[json = "member_limit"]
    #[default = DEFAULT_MEMBER_LIMIT]
    member_limit_override: Option<i32>,
    #[json = "group_limit"]
    #[default = DEFAULT_GROUP_LIMIT]
    group_limit_override: Option<i32>,
    #[json = "case_sensitive_proxy_tags"]
    case_sensitive_proxy_tags: bool,
    #[json = "proxy_error_message_enabled"]
    proxy_error_message_enabled: bool,
    #[json = "hid_display_split"]
    hid_display_split: bool,
    #[json = "hid_display_caps"]
    hid_display_caps: bool,
    #[json = "hid_list_padding"]
    hid_list_padding: HidPadFormat,
    #[json = "proxy_switch"]
    proxy_switch: ProxySwitchAction,
    #[json = "name_format"]
    #[default = "{name} {tag}".to_string()]
    name_format: Option<String>,
    #[json = "description_templates"]
    description_templates: Vec<String>,
}
