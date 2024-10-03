pub async fn get_stats(pool: &sqlx::postgres::PgPool) -> anyhow::Result<Counts> {
    let counts: Counts = sqlx::query_as("select * from info").fetch_one(pool).await?;
    Ok(counts)
}

#[derive(serde::Serialize, sqlx::FromRow)]
pub struct Counts {
    pub system_count: i64,
    pub member_count: i64,
    pub group_count: i64,
    pub switch_count: i64,
    pub message_count: i64,
}
