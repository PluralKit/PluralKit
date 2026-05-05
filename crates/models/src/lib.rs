mod _util;

mod group;
mod member;
mod switch;
mod system;
mod system_config;

pub use group::*;
pub use member::*;
pub use switch::*;
pub use system::*;
pub use system_config::*;

#[derive(serde::Serialize, Debug, Clone)]
#[serde(rename_all = "snake_case")]
pub enum PrivacyLevel {
    Public,
    Private,
}

// this sucks, put it somewhere else
use sqlx::{Database, Decode, Postgres, Type, postgres::PgTypeInfo};
use std::error::Error;
_util::fake_enum_impls!(PrivacyLevel);

impl From<i32> for PrivacyLevel {
    fn from(value: i32) -> Self {
        match value {
            1 => PrivacyLevel::Public,
            2 => PrivacyLevel::Private,
            _ => unreachable!(),
        }
    }
}
