use uuid::Uuid;

pub async fn legacy_token_auth(
    pool: &sqlx::postgres::PgPool,
    token: &str,
) -> anyhow::Result<Option<i32>> {
    let mut system: Vec<LegacyTokenDbResponse> =
        sqlx::query_as("select id from systems where token = $1")
            .bind(token)
            .fetch_all(pool)
            .await?;
    Ok(if let Some(system) = system.pop() {
        Some(system.id)
    } else {
        None
    })
}

#[derive(sqlx::FromRow)]
struct LegacyTokenDbResponse {
    id: i32,
}

pub async fn app_token_auth(
	pool: &sqlx::postgres::PgPool,
	token: &str,
) -> anyhow::Result<Option<Uuid>> {
	let mut app: Vec<AppTokenDbResponse> =
        sqlx::query_as("select id from external_apps where api_rl_token = $1")
            .bind(token)
            .fetch_all(pool)
            .await?;
    Ok(if let Some(app) = app.pop() {
        Some(app.id)
    } else {
        None
    })
}

#[derive(sqlx::FromRow)]
struct AppTokenDbResponse {
    id: Uuid,
}
