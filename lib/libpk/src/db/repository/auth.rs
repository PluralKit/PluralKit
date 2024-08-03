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
