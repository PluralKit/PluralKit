mod _util;

macro_rules! model {
    ($n:ident) => {
        mod $n;
        pub use $n::*;
    };
}

model!(system);
model!(system_config);

// todo: move these into model files later
pub type MemberId = i32;
pub type GroupId = i32;
pub type SwitchId = i32;

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
