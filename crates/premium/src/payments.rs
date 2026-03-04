use std::collections::HashSet;

use api::ApiContext;
use askama::Template;
use axum::{
    Json,
    extract::State,
    http::{HeaderMap, StatusCode},
    response::{IntoResponse, Response},
};
use pk_macros::api_endpoint;
use serde::{Deserialize, Serialize};
use sqlx::postgres::Postgres;
use tracing::{error, info, warn};

use crate::fail;

fn html_escape(s: &str) -> String {
    s.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
        .replace('\'', "&#x27;")
}

const SUBSCRIPTION_QUERY: &str = r#"
    select
        p.id, p.provider, p.provider_id, p.email, p.system_id,
        s.hid as system_hid, s.name as system_name,
        p.status, p.next_renewal_at
    from premium_subscriptions p
    left join systems s on p.system_id = s.id
"#;

const MONTHLY_ID_CHANGES: i32 = 10; // or something

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
pub struct StripeSubscription {
    pub id: String,
    pub status: String,
    pub current_period_end: Option<i64>,
    pub cancel_at_period_end: bool,
    pub customer: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct SubscriptionInfo {
    pub db: Option<DbSubscription>,
    pub stripe: Option<StripeSubscription>,
}

impl SubscriptionInfo {
    pub fn subscription_id(&self) -> &str {
        if let Some(stripe) = &self.stripe {
            &stripe.id
        } else if let Some(db) = &self.db {
            &db.provider_id
        } else {
            "unknown"
        }
    }

    pub fn status(&self) -> String {
        if let Some(stripe) = &self.stripe {
            if stripe.cancel_at_period_end {
                if let Some(end) = stripe.current_period_end {
                    if let Some(dt) = chrono::DateTime::from_timestamp(end, 0) {
                        return format!("expires {}", dt.format("%Y-%m-%d"));
                    }
                }
            }
            stripe.status.to_lowercase()
        } else if let Some(db) = &self.db {
            db.status.clone().unwrap_or_else(|| "unknown".to_string())
        } else {
            "unknown".to_string()
        }
    }

    pub fn next_renewal(&self) -> String {
        if let Some(stripe) = &self.stripe {
            if stripe.cancel_at_period_end {
                return "-".to_string();
            }
            if let Some(end) = stripe.current_period_end {
                if let Some(dt) = chrono::DateTime::from_timestamp(end, 0) {
                    return dt.format("%Y-%m-%d").to_string();
                }
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
                    // todo(premium): this is terrible
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
        // todo(premium): support linking/unlinking
    }

    pub fn is_cancellable(&self) -> bool {
        if let Some(stripe) = &self.stripe {
            if stripe.cancel_at_period_end {
                return false;
            }
            matches!(stripe.status.as_str(), "active" | "past_due")
        } else if let Some(db) = &self.db {
            matches!(db.status.as_deref(), Some("active") | Some("past_due"))
        } else {
            false
        }
    }
}

/// Get the current_period_end from the first subscription item.
/// In the newer Stripe API, current_period_end lives on SubscriptionItem, not Subscription.
fn get_current_period_end(sub: &stripe_shared::Subscription) -> Option<i64> {
    sub.items.data.first().map(|item| item.current_period_end)
}

async fn fetch_stripe_subscriptions(
    client: &stripe::Client,
    email: &str,
) -> anyhow::Result<Vec<StripeSubscription>> {
    let customers = stripe_core::customer::ListCustomer::new()
        .email(email)
        .send(client)
        .await?;

    let mut stripe_subs = Vec::new();
    for customer in customers.data {
        let subs = stripe_billing::subscription::ListSubscription::new()
            .customer(customer.id.to_string())
            .send(client)
            .await?;
        for sub in subs.data {
            stripe_subs.push(StripeSubscription {
                id: sub.id.to_string(),
                status: sub.status.as_str().to_string(),
                current_period_end: get_current_period_end(&sub),
                cancel_at_period_end: sub.cancel_at_period_end,
                customer: sub.customer.id().to_string(),
            });
        }
    }

    Ok(stripe_subs)
}

pub async fn fetch_subscriptions_for_email(
    ctx: &ApiContext,
    email: &str,
) -> anyhow::Result<Vec<SubscriptionInfo>> {
    let db_subs = get_subscriptions_by_email(ctx, email).await?;

    let config = libpk::config.premium();
    // todo(premium): maybe don't recreate it
    let client = stripe::Client::new(&config.stripe_secret_key);

    let stripe_subs = match fetch_stripe_subscriptions(&client, email).await {
        Ok(subs) => subs,
        Err(err) => {
            error!(
                ?err,
                "failed to fetch subscriptions from Stripe for {}", email
            );
            vec![]
        }
    };

    let mut results: Vec<SubscriptionInfo> = Vec::new();
    let mut found_ids: HashSet<String> = HashSet::new();

    for db_sub in &db_subs {
        let stripe_match = stripe_subs.iter().find(|s| s.id == db_sub.provider_id);

        if let Some(stripe) = stripe_match {
            found_ids.insert(stripe.id.clone());
            results.push(SubscriptionInfo {
                db: Some(db_sub.clone()),
                stripe: Some(stripe.clone()),
            });
        } else {
            results.push(SubscriptionInfo {
                db: Some(db_sub.clone()),
                stripe: None,
            });
        }
    }

    for stripe_sub in stripe_subs {
        if !found_ids.contains(&stripe_sub.id) {
            results.push(SubscriptionInfo {
                db: None,
                stripe: Some(stripe_sub),
            });
        }
    }

    results.retain(|sub| sub.status() != "canceled");

    Ok(results)
}

pub async fn create_checkout_url(email: &str, system_id: i32) -> anyhow::Result<String> {
    let config = libpk::config.premium();
    let client = stripe::Client::new(&config.stripe_secret_key);

    let success_url = format!("{}/", config.base_url);
    let cancel_url = format!("{}/", config.base_url);
    let system_id_str = system_id.to_string();

    // Use raw reqwest to set the managed payments preview header,
    // which async-stripe's Client doesn't support.
    let mut form_params = vec![
        ("mode", "subscription"),
        ("line_items[0][price]", config.stripe_price_id.as_str()),
        ("line_items[0][quantity]", "1"),
        ("success_url", success_url.as_str()),
        ("cancel_url", cancel_url.as_str()),
        ("metadata[email]", email),
        ("metadata[system_id]", system_id_str.as_str()),
        ("subscription_data[metadata][email]", email),
        (
            "subscription_data[metadata][system_id]",
            system_id_str.as_str(),
        ),
        ("managed_payments[enabled]", "true"),
    ];

    // fetch existing customer by email instead of just providing the email to the checkout form
    // (would create a new customer even if one is existing)
    let customer_id = stripe_core::customer::ListCustomer::new()
        .email(email)
        .send(&client)
        .await?
        .data
        .into_iter()
        .next()
        .map(|c| c.id.to_string());

    if let Some(ref id) = customer_id {
        form_params.push(("customer", id.as_str()));
    } else {
        form_params.push(("customer_email", email));
    }

    let response = reqwest::Client::new()
        .post("https://api.stripe.com/v1/checkout/sessions")
        .bearer_auth(&config.stripe_secret_key)
        .header(
            "Stripe-Version",
            "2025-03-31.basil; managed_payments_preview=v1",
        )
        .form(&form_params)
        .send()
        .await?;

    if !response.status().is_success() {
        let status = response.status();
        let body = response.text().await.unwrap_or_default();
        anyhow::bail!("Stripe API error: {} - {}", status, body);
    }

    #[derive(Deserialize)]
    struct CheckoutSessionResponse {
        url: Option<String>,
    }

    let session: CheckoutSessionResponse = response.json().await?;
    session
        .url
        .ok_or_else(|| anyhow::anyhow!("checkout session missing URL"))
}

#[derive(Debug, Deserialize)]
pub struct CheckoutRequest {
    pub csrf_token: String,
    pub system_id: i32,
}

#[derive(Debug, Serialize)]
pub struct CheckoutUrlResponse {
    pub url: String,
}

#[api_endpoint]
pub async fn checkout(
    axum::Extension(session): axum::Extension<crate::auth::AuthState>,
    Json(req): Json<CheckoutRequest>,
) -> Response {
    if req.csrf_token != session.csrf_token {
        return Ok((StatusCode::FORBIDDEN, "invalid csrf token").into_response());
    }

    match create_checkout_url(&session.email, req.system_id).await {
        Ok(url) => Ok(Json(CheckoutUrlResponse { url }).into_response()),
        Err(err) => {
            error!(?err, "failed to create checkout URL");
            Ok((
                StatusCode::INTERNAL_SERVER_ERROR,
                "failed to create checkout",
            )
                .into_response())
        }
    }
}

async fn save_subscription(
    ctx: &ApiContext,
    subscription_id: &str,
    email: &str,
    status: &str,
    next_renewal_at: Option<&str>,
    system_id: Option<i32>,
) -> anyhow::Result<()> {
    sqlx::query::<Postgres>(
        r#"
        insert into premium_subscriptions (provider, provider_id, email, system_id, status, next_renewal_at)
        values ('stripe', $1, $2, $3, $4, $5)
        on conflict (provider, provider_id) do update set
            status = excluded.status,
            next_renewal_at = excluded.next_renewal_at,
            system_id = coalesce(excluded.system_id, premium_subscriptions.system_id)
        "#,
    )
    .bind(subscription_id)
    .bind(email)
    .bind(system_id)
    .bind(status)
    .bind(next_renewal_at)
    .execute(&ctx.db)
    .await?;

    Ok(())
}

fn extract_subscription_fields(
    sub: &stripe_shared::Subscription,
) -> (String, Option<String>, Option<i32>) {
    let status = if sub.cancel_at_period_end {
        "canceling".to_string()
    } else {
        sub.status.as_str().to_string()
    };
    let renewal_str = get_current_period_end(sub)
        .and_then(|ts| chrono::DateTime::from_timestamp(ts, 0))
        .map(|dt| dt.to_rfc3339());
    let system_id = sub
        .metadata
        .get("system_id")
        .and_then(|s| s.parse::<i32>().ok());
    (status, renewal_str, system_id)
}

/// Extract a subscription ID from the raw webhook JSON payload.
/// Handles both old (`data.object.subscription`) and new
/// (`data.object.parent.subscription_details.subscription`) API layouts.
fn extract_sub_id_from_payload(payload: &serde_json::Value) -> Option<String> {
    let obj = &payload["data"]["object"];

    // the object itself is a subscription
    if obj["object"].as_str() == Some("subscription") {
        return obj["id"].as_str().map(String::from);
    }

    // new API: nested under parent.subscription_details
    if let Some(id) = obj["parent"]["subscription_details"]["subscription"].as_str() {
        return Some(id.to_string());
    }

    // old API: top-level field (string or expandable object)
    match &obj["subscription"] {
        serde_json::Value::String(s) => Some(s.clone()),
        v if v.is_object() => v["id"].as_str().map(String::from),
        _ => None,
    }
}

#[api_endpoint]
pub async fn webhook(State(ctx): State<ApiContext>, headers: HeaderMap, body: String) -> Response {
    let Some(signature) = headers
        .get("Stripe-Signature")
        .and_then(|h| h.to_str().ok())
    else {
        return Ok(StatusCode::BAD_REQUEST.into_response());
    };

    let config = libpk::config.premium();

    // verify signature only — we parse the payload ourselves to avoid
    // deserialization failures from SDK/API version mismatches
    if let Err(err) =
        stripe_webhook::Webhook::construct_event(&body, signature, &config.stripe_webhook_secret)
    {
        error!(?err, "failed to verify webhook signature");
        return Ok(StatusCode::BAD_REQUEST.into_response());
    }

    let payload: serde_json::Value = match serde_json::from_str(&body) {
        Ok(v) => v,
        Err(err) => {
            error!(?err, "failed to parse webhook body");
            return Ok(StatusCode::BAD_REQUEST.into_response());
        }
    };

    let event_type = payload["type"].as_str().unwrap_or("");
    let client = stripe::Client::new(&config.stripe_secret_key);

    match event_type {
        "checkout.session.completed" => {
            let Some(sub_id_str) = extract_sub_id_from_payload(&payload) else {
                info!("checkout session completed without subscription");
                return Ok("".into_response());
            };

            let sub_id: stripe_shared::SubscriptionId = match sub_id_str.parse() {
                Ok(id) => id,
                Err(err) => {
                    fail!(?err, "failed to parse subscription id {}", sub_id_str);
                }
            };

            let sub = match stripe_billing::subscription::RetrieveSubscription::new(&sub_id)
                .send(&client)
                .await
            {
                Ok(s) => s,
                Err(err) => {
                    fail!(?err, "failed to retrieve subscription {}", sub_id);
                }
            };

            let email = sub.metadata.get("email").cloned().unwrap_or_default();
            let (status, renewal_str, system_id) = extract_subscription_fields(&sub);

            if let Err(err) = save_subscription(
                &ctx,
                &sub_id.to_string(),
                &email,
                &status,
                renewal_str.as_deref(),
                system_id,
            )
            .await
            {
                fail!(?err, "failed to save subscription {}", sub_id);
            }

            info!("saved subscription {} with status {}", sub_id, status);
        }
        "invoice.paid" => {
            let Some(sub_id_str) = extract_sub_id_from_payload(&payload) else {
                info!("invoice.paid without subscription, ignoring");
                return Ok("".into_response());
            };

            let sub_id: stripe_shared::SubscriptionId = match sub_id_str.parse() {
                Ok(id) => id,
                Err(err) => {
                    fail!(?err, "failed to parse subscription id {}", sub_id_str);
                }
            };

            // fetch full subscription from Stripe so we can upsert even if
            // checkout.session.completed hasn't arrived yet
            let sub = match stripe_billing::subscription::RetrieveSubscription::new(&sub_id)
                .send(&client)
                .await
            {
                Ok(s) => s,
                Err(err) => {
                    fail!(
                        ?err,
                        "failed to retrieve subscription {} for invoice.paid",
                        sub_id
                    );
                }
            };

            let email = sub.metadata.get("email").cloned().unwrap_or_default();
            let (status, renewal_str, system_id) = extract_subscription_fields(&sub);

            // upsert subscription row (creates it if checkout.session.completed hasn't fired yet)
            if let Err(err) = save_subscription(
                &ctx,
                &sub_id.to_string(),
                &email,
                &status,
                renewal_str.as_deref(),
                system_id,
            )
            .await
            {
                fail!(
                    ?err,
                    "failed to save subscription {} on invoice.paid",
                    sub_id
                );
            }

            // look up the db row to get the internal id for allowances
            let row = sqlx::query_as::<Postgres, (i32,)>(
                "select id from premium_subscriptions where provider = 'stripe' and provider_id = $1",
            )
            .bind(sub_id.to_string())
            .fetch_one(&ctx.db)
            .await;

            match row {
                Ok((db_id,)) => {
                    // todo(premium): make this better
                    if let Err(err) = sqlx::query::<Postgres>(
                        r#"
                        insert into premium_allowances (subscription_id, system_id, id_changes_remaining)
                        values ($1, $2, $3)
                        on conflict (subscription_id) do update set
                            system_id = coalesce(excluded.system_id, premium_allowances.system_id),
                            id_changes_remaining = premium_allowances.id_changes_remaining + excluded.id_changes_remaining
                        "#,
                    )
                    .bind(db_id)
                    .bind(system_id)
                    .bind(MONTHLY_ID_CHANGES)
                    .execute(&ctx.db)
                    .await
                    {
                        fail!(?err, "failed to update premium_allowances for subscription {}", sub_id);
                    }

                    info!(
                        "updated premium_allowances for subscription {} (db id {})",
                        sub_id, db_id
                    );
                }
                Err(err) => {
                    fail!(?err, "failed to look up subscription {}", sub_id);
                }
            }
        }
        "customer.subscription.updated" | "customer.subscription.deleted" => {
            let Some(sub_id_str) = extract_sub_id_from_payload(&payload) else {
                warn!("subscription event without subscription id");
                return Ok("".into_response());
            };

            let sub_id: stripe_shared::SubscriptionId = match sub_id_str.parse() {
                Ok(id) => id,
                Err(err) => {
                    fail!(?err, "failed to parse subscription id {}", sub_id_str);
                }
            };

            let sub = match stripe_billing::subscription::RetrieveSubscription::new(&sub_id)
                .send(&client)
                .await
            {
                Ok(s) => s,
                Err(err) => {
                    fail!(?err, "failed to retrieve subscription {}", sub_id);
                }
            };

            let email = sub.metadata.get("email").cloned().unwrap_or_default();
            let (status, renewal_str, system_id) = extract_subscription_fields(&sub);

            if let Err(err) = save_subscription(
                &ctx,
                &sub_id.to_string(),
                &email,
                &status,
                renewal_str.as_deref(),
                system_id,
            )
            .await
            {
                fail!(?err, "failed to save subscription {}", sub_id);
            }

            info!("saved subscription {} with status {}", sub_id, status);
        }
        _ => {
            warn!("unhandled stripe event type {}", event_type);
        }
    }

    Ok("".into_response())
}

pub async fn cancel_subscription(subscription_id: &str) -> anyhow::Result<()> {
    let config = libpk::config.premium();
    let client = stripe::Client::new(&config.stripe_secret_key);

    let sub_id: stripe_shared::SubscriptionId = subscription_id.parse()?;

    stripe_billing::subscription::UpdateSubscription::new(&sub_id)
        .cancel_at_period_end(true)
        .send(&client)
        .await?;

    Ok(())
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
        Ok(_) => {
            info!(
                "cancelled subscription {} for {}",
                form.subscription_id, session.email
            );
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
