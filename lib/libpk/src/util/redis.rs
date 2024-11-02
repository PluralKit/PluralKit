use fred::error::RedisError;

pub trait RedisErrorExt<T> {
    fn to_option_or_error(self) -> Result<Option<T>, RedisError>;
}

impl<T> RedisErrorExt<T> for Result<T, RedisError> {
    fn to_option_or_error(self) -> Result<Option<T>, RedisError> {
        match self {
            Ok(v) => Ok(Some(v)),
            Err(error) if error.is_not_found() => Ok(None),
            Err(error) => Err(error),
        }
    }
}
