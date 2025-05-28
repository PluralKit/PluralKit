// postgres enums created in c# pluralkit implementations are "fake", i.e. they
// are actually ints in the database rather than postgres enums, because dapper
// does not support postgres enums
// here, we add some impls to support this kind of enum in sqlx
// there is probably a better way to do this, but works for now.
// note: caller needs to implement From<i32> for their type
macro_rules! fake_enum_impls {
    ($n:ident) => {
        impl Type<Postgres> for $n {
            fn type_info() -> PgTypeInfo {
                PgTypeInfo::with_name("INT4")
            }
        }

        impl From<$n> for i32 {
            fn from(enum_value: $n) -> Self {
                enum_value as i32
            }
        }

        impl<'r, DB: Database> Decode<'r, DB> for $n
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
    };
}

pub(crate) use fake_enum_impls;

macro_rules! privacy_lookup {
    ($v:expr, $vprivacy:expr, $lookup_level:expr) => {
        if matches!($vprivacy, crate::PrivacyLevel::Public)
            || matches!($lookup_level, crate::PrivacyLevel::Private)
        {
            Some($v)
        } else {
            None
        }
    };
}

pub(crate) use privacy_lookup;
