use std::error::Error;

use model_macros::pk_model;

use chrono::NaiveDateTime;
use sqlx::{postgres::PgTypeInfo, Database, Decode, Postgres, Type};
use uuid::Uuid;

use crate::_util::fake_enum_impls;

// todo: fix this
pub type SystemId = i32;

// todo: move this
#[derive(serde::Serialize, Debug, Clone)]
#[serde(rename_all = "snake_case")]
pub enum PrivacyLevel {
    Public,
    Private,
}

fake_enum_impls!(PrivacyLevel);

impl From<i32> for PrivacyLevel {
    fn from(value: i32) -> Self {
        match value {
            1 => PrivacyLevel::Public,
            2 => PrivacyLevel::Private,
            _ => unreachable!(),
        }
    }
}

#[pk_model]
struct System {
    id: SystemId,
    #[json = "id"]
    #[private_patchable]
    hid: String,
    #[json = "uuid"]
    uuid: Uuid,
    #[json = "name"]
    name: Option<String>,
    #[json = "description"]
    description: Option<String>,
    #[json = "tag"]
    tag: Option<String>,
    #[json = "pronouns"]
    pronouns: Option<String>,
    #[json = "avatar_url"]
    avatar_url: Option<String>,
    #[json = "banner_image"]
    banner_image: Option<String>,
    #[json = "color"]
    color: Option<String>,
    token: Option<String>,
    #[json = "webhook_url"]
    webhook_url: Option<String>,
    webhook_token: Option<String>,
    #[json = "created"]
    created: NaiveDateTime,
    #[privacy]
    name_privacy: PrivacyLevel,
    #[privacy]
    avatar_privacy: PrivacyLevel,
    #[privacy]
    description_privacy: PrivacyLevel,
    #[privacy]
    banner_privacy: PrivacyLevel,
    #[privacy]
    member_list_privacy: PrivacyLevel,
    #[privacy]
    front_privacy: PrivacyLevel,
    #[privacy]
    front_history_privacy: PrivacyLevel,
    #[privacy]
    group_list_privacy: PrivacyLevel,
    #[privacy]
    pronoun_privacy: PrivacyLevel,
}
