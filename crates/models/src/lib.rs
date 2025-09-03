mod _util;

macro_rules! model {
    ($n:ident) => {
        mod $n;
        pub use $n::*;
    };
}

model!(system);
model!(system_config);
model!(member);
model!(group);

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

impl From<PrivacyLevel> for sea_query::Value {
    fn from(level: PrivacyLevel) -> sea_query::Value {
        match level {
            PrivacyLevel::Public => sea_query::Value::Int(Some(1)),
            PrivacyLevel::Private => sea_query::Value::Int(Some(2)),
        }
    }
}

#[derive(serde::Serialize, Debug, Clone)]
pub enum ValidationError {
    Simple { key: String, value: String },
}

impl ValidationError {
    fn new(key: &str) -> Self {
        Self::simple(key, "is invalid")
    }

    fn simple(key: &str, value: &str) -> Self {
        Self::Simple {
            key: key.to_string(),
            value: value.to_string(),
        }
    }
}
