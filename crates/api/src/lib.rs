mod auth;
pub mod error;
pub mod middleware;
pub mod util;

#[derive(Clone)]
pub struct ApiContext {
    pub db: sqlx::postgres::PgPool,
    pub redis: fred::clients::RedisPool,
}
