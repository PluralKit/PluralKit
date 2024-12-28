use std::error::Error;

use model_macros::pk_model;

use chrono::NaiveDateTime;
use sqlx::{postgres::PgTypeInfo, Database, Decode, Postgres, Type};
use uuid::Uuid;

// todo: fix this
pub type SystemId = i32;

// // todo: move this
#[derive(serde::Serialize, Debug, Clone)]
pub enum PrivacyLevel {
    #[serde(rename = "public")]
    Public = 1,
    #[serde(rename = "private")]
    Private = 2,
}

impl Type<Postgres> for PrivacyLevel {
    fn type_info() -> PgTypeInfo {
        PgTypeInfo::with_name("INT4")
    }
}

impl From<PrivacyLevel> for i32 {
    fn from(enum_value: PrivacyLevel) -> Self {
        enum_value as i32
    }
}

impl From<i32> for PrivacyLevel {
    fn from(value: i32) -> Self {
        match value {
            1 => PrivacyLevel::Public,
            2 => PrivacyLevel::Private,
            _ => unimplemented!(),
        }
    }
}

struct MyType;

impl<'r, DB: Database> Decode<'r, DB> for PrivacyLevel
where
    i32: Decode<'r, DB>,
{
    fn decode(
        value: <DB as Database>::ValueRef<'r>,
    ) -> Result<Self, Box<dyn Error + 'static + Send + Sync>> {
        let value = <i32 as Decode<DB>>::decode(value)?;
        Ok(Self::from(value))
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
