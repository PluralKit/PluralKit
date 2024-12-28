pub async fn get_stats(pool: &sqlx::postgres::PgPool) -> anyhow::Result<Counts> {
    let counts: Counts = sqlx::query_as("select * from info").fetch_one(pool).await?;
    Ok(counts)
}

pub async fn insert_stats(
    pool: &sqlx::postgres::PgPool,
    table: &str,
    value: i64,
) -> anyhow::Result<()> {
    // danger sql injection
    sqlx::query(format!("insert into {table} values (now(), $1)").as_str())
        .bind(value)
        .execute(pool)
        .await?;
    Ok(())
}

#[derive(serde::Serialize, sqlx::FromRow)]
pub struct Counts {
    pub system_count: i64,
    pub member_count: i64,
    pub group_count: i64,
    pub switch_count: i64,
    pub message_count: i64,
}
