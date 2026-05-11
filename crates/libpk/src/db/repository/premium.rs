use serde::Serialize;

#[derive(sqlx::FromRow, Clone, Debug, Serialize)]
pub struct PremiumAllowances {
    pub status: Option<String>,
    pub next_renewal_at: Option<String>,
    pub id_changes_remaining: i32,
}

impl PremiumAllowances {
    pub fn is_active(&self) -> bool {
        matches!(
            self.status.as_deref(),
            Some("active" | "past_due" | "canceling" | "lifetime")
        )
    }
}

pub async fn get_system_premium(
    pool: &sqlx::PgPool,
    system_id: i32,
) -> anyhow::Result<Option<PremiumAllowances>> {
    Ok(sqlx::query_as(
        r#"select ps.status, ps.next_renewal_at,
                  coalesce(pa.id_changes_remaining, 0) as id_changes_remaining
           from premium_subscriptions ps
           left join premium_allowances pa on pa.subscription_id = ps.id
           where ps.system_id = $1 and ps.status != 'canceled'"#,
    )
    .bind(system_id)
    .fetch_optional(pool)
    .await?)
}
