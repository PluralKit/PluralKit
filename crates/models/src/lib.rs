mod _util;

macro_rules! model {
    ($n:ident) => {
        mod $n;
        pub use $n::*;
    };
}

model!(system);
model!(system_config);

#[derive(serde::Serialize, Debug, Clone)]
#[serde(rename_all = "snake_case")]
pub enum PrivacyLevel {
    Public,
    Private,
}

// this sucks, put it somewhere else
use sqlx::{postgres::PgTypeInfo, Database, Decode, Postgres, Type};
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
