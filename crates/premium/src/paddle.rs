use std::{collections::HashSet, vec};

use api::ApiContext;
use askama::Template;
use axum::{
    extract::State,
    http::{HeaderMap, StatusCode},
    response::{IntoResponse, Response},
};
use lazy_static::lazy_static;
use paddle_rust_sdk::{
    Paddle,
    entities::{Customer, Subscription},
    enums::{EventData, SubscriptionStatus},
    webhooks::MaximumVariance,
};
use pk_macros::api_endpoint;
use serde::Serialize;
use sqlx::postgres::Postgres;
use tracing::{error, info};

use crate::fail;

// ew
fn html_escape(s: &str) -> String {
    s.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
        .replace('\'', "&#x27;")
}

lazy_static! {
    static ref PADDLE_CLIENT: Paddle = {
        let config = libpk::config.premium();
        let base_url = if config.is_paddle_production {
            Paddle::PRODUCTION
        } else {
            Paddle::SANDBOX
        };
        Paddle::new(&config.paddle_api_key, base_url).expect("failed to create paddle client")
    };
}

pub async fn fetch_customer(customer_id: &str) -> anyhow::Result<Customer> {
    let customer = PADDLE_CLIENT.customer_get(customer_id).send().await?;
    Ok(customer.data)
}

const SUBSCRIPTION_QUERY: &str = r#"
    select
        p.id, p.provider, p.provider_id, p.email, p.system_id,
        s.hid as system_hid, s.name as system_name,
        p.status, p.next_renewal_at
    from premium_subscriptions p
    left join systems s on p.system_id = s.id
"#;

async fn get_subscriptions_by_email(
    ctx: &ApiContext,
    email: &str,
) -> anyhow::Result<Vec<DbSubscription>> {
    let query = format!("{} where p.email = $1", SUBSCRIPTION_QUERY);
    let subs = sqlx::query_as(&query)
        .bind(email)
        .fetch_all(&ctx.db)
        .await?;
    Ok(subs)
}

async fn get_subscription(
    ctx: &ApiContext,
    provider_id: &str,
    email: &str,
) -> anyhow::Result<Option<DbSubscription>> {
    let query = format!(
        "{} where p.provider_id = $1 and p.email = $2",
        SUBSCRIPTION_QUERY
    );
    let sub = sqlx::query_as(&query)
        .bind(provider_id)
        .bind(email)
        .fetch_optional(&ctx.db)
        .await?;
    Ok(sub)
}

#[derive(Debug, Clone, sqlx::FromRow, Serialize)]
pub struct DbSubscription {
    pub id: i32,
    pub provider: String,
    pub provider_id: String,
    pub email: String,
    pub system_id: Option<i32>,
    pub system_hid: Option<String>,
    pub system_name: Option<String>,
    pub status: Option<String>,
    pub next_renewal_at: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
pub struct SubscriptionInfo {
    pub db: Option<DbSubscription>,
    pub paddle: Option<Subscription>,
}

impl SubscriptionInfo {
    pub fn subscription_id(&self) -> &str {
        if let Some(paddle) = &self.paddle {
            paddle.id.as_ref()
        } else if let Some(db) = &self.db {
            &db.provider_id
        } else {
            "unknown"
        }
    }

    pub fn status(&self) -> String {
        if let Some(paddle) = &self.paddle {
            if let Some(ref scheduled) = paddle.scheduled_change {
                if matches!(
                    scheduled.action,
                    paddle_rust_sdk::enums::ScheduledChangeAction::Cancel
                ) {
                    return format!("expires {}", scheduled.effective_at.format("%Y-%m-%d"));
                }
            }
            format!("{:?}", paddle.status).to_lowercase()
        } else if let Some(db) = &self.db {
            db.status.clone().unwrap_or_else(|| "unknown".to_string())
        } else {
            "unknown".to_string()
        }
    }

    pub fn next_renewal(&self) -> String {
        if let Some(paddle) = &self.paddle {
            // if subscription is canceled, show next_billed_at as "ends at" date instead of "next renewal"
            if paddle.scheduled_change.as_ref().is_some_and(|s| {
                matches!(
                    s.action,
                    paddle_rust_sdk::enums::ScheduledChangeAction::Cancel
                )
            }) {
                return "-".to_string();
            }
            if let Some(next) = paddle.next_billed_at {
                return next.format("%Y-%m-%d").to_string();
            }
        }
        if let Some(db) = &self.db {
            if let Some(next) = &db.next_renewal_at {
                return next.split('T').next().unwrap_or(next).to_string();
            }
        }
        "-".to_string()
    }

    pub fn system_id_display(&self) -> String {
        if let Some(db) = &self.db {
            if let Some(hid) = &db.system_hid {
                if let Some(name) = &db.system_name {
                    // ew, this needs to be fixed
                    let escaped_name = html_escape(name);
                    return format!("{} (<code>{}</code>)", escaped_name, hid);
                }
                return format!("<code>{}</code>", hid);
            }
            if db.system_id.is_some() {
                return "unknown system (contact us at billing@pluralkit.me to fix this)"
                    .to_string();
            }
            return "not linked".to_string();
        }
        "not linked".to_string()
    }

    pub fn is_cancellable(&self) -> bool {
        if let Some(paddle) = &self.paddle {
            if paddle.scheduled_change.as_ref().is_some_and(|s| {
                matches!(
                    s.action,
                    paddle_rust_sdk::enums::ScheduledChangeAction::Cancel
                )
            }) {
                return false;
            }
            matches!(
                paddle.status,
                SubscriptionStatus::Active | SubscriptionStatus::PastDue
            )
        } else if let Some(db) = &self.db {
            matches!(db.status.as_deref(), Some("active") | Some("past_due"))
        } else {
            false
        }
    }
}

// this is slightly terrible, but works
// the paddle sdk is a mess which does not help
pub async fn fetch_subscriptions_for_email(
    ctx: &ApiContext,
    email: &str,
) -> anyhow::Result<Vec<SubscriptionInfo>> {
    let db_subs = get_subscriptions_by_email(ctx, email).await?;

    let mut paddle_subs: Vec<Subscription> = Vec::new();

    // there's no method to look up customer by email, so we have to do this nonsense
    let Some(customer) = PADDLE_CLIENT
        .customers_list()
        .emails([email])
        .send()
        .next()
        .await?
        .and_then(|v| v.data.into_iter().next())
    else {
        return Ok(vec![]);
    };

    // why
    let mut temp_paddle_for_sub_list = PADDLE_CLIENT.subscriptions_list();
    let mut subs_pages = temp_paddle_for_sub_list.customer_id([customer.id]).send();
    while let Some(subs_page) = subs_pages.next().await? {
        paddle_subs.extend(subs_page.data);
    }

    let mut results: Vec<SubscriptionInfo> = Vec::new();
    let mut found_ids: HashSet<String> = HashSet::new();

    for db_sub in &db_subs {
        let paddle_match = paddle_subs
            .iter()
            .find(|p| p.id.as_ref() == db_sub.provider_id);

        if let Some(paddle) = paddle_match {
            found_ids.insert(paddle.id.as_ref().to_string());
            results.push(SubscriptionInfo {
                db: Some(db_sub.clone()),
                paddle: Some(paddle.clone()),
            });
        } else {
            results.push(SubscriptionInfo {
                db: Some(db_sub.clone()),
                paddle: None,
            });
        }
    }

    for paddle_sub in paddle_subs {
        if !found_ids.contains(paddle_sub.id.as_ref()) {
            results.push(SubscriptionInfo {
                db: None,
                paddle: Some(paddle_sub),
            });
        }
    }

    // todo: show some error if a sub is only in db/provider but not both

    // todo: we may want to show canceled subscriptions in the future
    results.retain(|sub| sub.status() != "canceled");

    Ok(results)
}

async fn save_subscription(
    ctx: &ApiContext,
    sub: &Subscription,
    email: &str,
) -> anyhow::Result<()> {
    let status = format!("{:?}", sub.status).to_lowercase();
    let next_renewal_at = sub.next_billed_at.map(|dt| dt.to_rfc3339());
    let system_id: Option<i32> = sub
        .custom_data
        .as_ref()
        .and_then(|d| d.get("system_id"))
        .and_then(|v| v.as_i64())
        .map(|v| v as i32);

    sqlx::query::<Postgres>(
        r#"
        insert into premium_subscriptions (provider, provider_id, email, system_id, status, next_renewal_at)
        values ('paddle', $1, $2, $3, $4, $5)
        on conflict (provider, provider_id) do update set
            status = excluded.status,
            next_renewal_at = excluded.next_renewal_at
        "#,
    )
    .bind(sub.id.as_ref())
    .bind(email)
    .bind(system_id)
    .bind(&status)
    .bind(&next_renewal_at)
    .execute(&ctx.db)
    .await?;

    // if has a linked system, also update system_config
    // just in case we get out of order webhooks, never reduce the premium_until
    // todo: this will obviously break if we refund someone's subscription
    if let Some(system_id) = system_id {
        if matches!(sub.status, SubscriptionStatus::Active) {
            if let Some(next_billed_at) = sub.next_billed_at {
                let premium_until = next_billed_at.naive_utc();
                sqlx::query::<Postgres>(
                    r#"
                    update system_config set
                        premium_until = greatest(system_config.premium_until, $2)
                    where system = $1
                    "#,
                )
                .bind(system_id)
                .bind(premium_until)
                .execute(&ctx.db)
                .await?;

                info!(
                    "updated premium_until for system {} to {}",
                    system_id, premium_until
                );
            }
        }
    }

    Ok(())
}

#[api_endpoint]
pub async fn webhook(State(ctx): State<ApiContext>, headers: HeaderMap, body: String) -> Response {
    let Some(signature) = headers
        .get("paddle-signature")
        .and_then(|h| h.to_str().ok())
    else {
        return Ok(StatusCode::BAD_REQUEST.into_response());
    };

    match match Paddle::unmarshal(
        body,
        &libpk::config.premium().paddle_webhook_secret,
        signature,
        MaximumVariance::default(),
    ) {
        Ok(event) => event,
        Err(err) => {
            error!(?err, "failed to unmarshal paddle data");
            return Ok(StatusCode::BAD_REQUEST.into_response());
        }
    }
    .data
    {
        EventData::SubscriptionCreated(sub)
        | EventData::SubscriptionActivated(sub)
        | EventData::SubscriptionUpdated(sub) => {
            match sub.status {
                SubscriptionStatus::Trialing => {
                    error!(
                        "got status trialing for subscription {}, this should never happen",
                        sub.id
                    );
                    return Ok("".into_response());
                }
                SubscriptionStatus::Active
                | SubscriptionStatus::Canceled
                | SubscriptionStatus::PastDue
                | SubscriptionStatus::Paused => {}
                unk => {
                    error!("got unknown status {unk:?} for subscription {}", sub.id);
                    return Ok("".into_response());
                }
            }

            let email = match fetch_customer(sub.customer_id.as_ref()).await {
                Ok(cus) => cus.email,
                Err(err) => {
                    fail!(
                        ?err,
                        "failed to fetch customer email for subscription {}",
                        sub.id
                    );
                }
            };

            if let Err(err) = save_subscription(&ctx, &sub, &email).await {
                fail!(?err, "failed to save subscription {}", sub.id);
            }

            info!("saved subscription {} with status {:?}", sub.id, sub.status);
        }
        _ => {}
    }

    Ok("".into_response())
}

pub async fn cancel_subscription(subscription_id: &str) -> anyhow::Result<Subscription> {
    let result = PADDLE_CLIENT
        .subscription_cancel(subscription_id)
        .send()
        .await?;
    Ok(result.data)
}

#[api_endpoint]
pub async fn cancel(
    State(ctx): State<ApiContext>,
    axum::Extension(session): axum::Extension<crate::auth::AuthState>,
    axum::Form(form): axum::Form<CancelForm>,
) -> Response {
    if form.csrf_token != session.csrf_token {
        return Ok((StatusCode::FORBIDDEN, "invalid csrf token").into_response());
    }

    let db_sub = get_subscription(&ctx, &form.subscription_id, &session.email)
        .await
        .map_err(|e| {
            error!(?e, "failed to fetch subscription from db");
            crate::error::GENERIC_SERVER_ERROR
        })?;

    if db_sub.is_none() {
        return Ok((
            StatusCode::FORBIDDEN,
            "subscription not found or not owned by you",
        )
            .into_response());
    }

    match cancel_subscription(&form.subscription_id).await {
        Ok(sub) => {
            info!("cancelled subscription {} for {}", sub.id, session.email);
            Ok(axum::response::Redirect::to("/").into_response())
        }
        Err(err) => {
            fail!(
                ?err,
                "failed to cancel subscription {}",
                form.subscription_id
            );
        }
    }
}

#[derive(serde::Deserialize)]
pub struct CancelForm {
    pub csrf_token: String,
    pub subscription_id: String,
}

#[derive(serde::Deserialize)]
pub struct CancelQuery {
    pub id: String,
}

pub async fn cancel_page(
    State(ctx): State<ApiContext>,
    axum::Extension(session): axum::Extension<crate::auth::AuthState>,
    axum::extract::Query(query): axum::extract::Query<CancelQuery>,
) -> Response {
    let subscriptions = match fetch_subscriptions_for_email(&ctx, &session.email).await {
        Ok(subs) => subs,
        Err(e) => {
            error!(?e, "failed to fetch subscriptions");
            return (
                StatusCode::INTERNAL_SERVER_ERROR,
                "failed to fetch subscriptions",
            )
                .into_response();
        }
    };

    let subscription = subscriptions
        .into_iter()
        .find(|s| s.subscription_id() == query.id);

    let Some(subscription) = subscription else {
        return (
            StatusCode::FORBIDDEN,
            "subscription not found or not owned by you",
        )
            .into_response();
    };

    axum::response::Html(
        crate::web::Cancel {
            csrf_token: session.csrf_token,
            subscription,
        }
        .render()
        .unwrap(),
    )
    .into_response()
}
