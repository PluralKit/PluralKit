use moka::future::Cache;
use once_cell::sync::Lazy;
use tracing::info;
use twilight_http::Client;
use twilight_model::channel::{embed::Embed, Webhook, WebhookType};

// space for 1 million is probably way overkill, this is a LRU cache so it's okay to evict occasionally
static WEBHOOK_CACHE: Lazy<Cache<u64, CachedWebhook>> = Lazy::new(|| Cache::new(1024 * 1024));
const WEBHOOK_NAME: &str = "PluralKit Proxy Webhook";

#[derive(Debug, Clone)]
pub struct CachedWebhook {
    pub id: u64,
    pub token: String,
}

pub async fn get_webhook_cached(http: &Client, channel_id: u64) -> anyhow::Result<CachedWebhook> {
    let res = WEBHOOK_CACHE
        .try_get_with(channel_id, fetch_or_create_pk_webhook(http, channel_id))
        .await;

    // todo: what happens if fetch_or_create_pk_webhook errors? i think moka handles it properly and just retries
    // but i'm not entiiiirely sure
    // https://docs.rs/moka/0.8.5/moka/future/struct.Cache.html#method.try_get_with

    // error is Arc<Error> here and it's hard to convert that into an owned ref so we just make a new error lmao
    res.map_err(|_e| anyhow::anyhow!(
        "could not fetch webhook: {}", _e
    ))
}

async fn fetch_or_create_pk_webhook(
    http: &Client,
    channel_id: u64,
) -> anyhow::Result<CachedWebhook> {
    match fetch_pk_webhook(http, channel_id).await? {
        Some(hook) => Ok(hook),
        None => create_pk_webhook(http, channel_id).await,
    }
}

async fn fetch_pk_webhook(http: &Client, channel_id: u64) -> anyhow::Result<Option<CachedWebhook>> {
    info!("cache miss, fetching webhook for channel {}", channel_id);

    let webhooks = http
        .channel_webhooks(channel_id.try_into().unwrap())
        .exec()
        .await?
        .models()
        .await?;

    webhooks
        .iter()
        .find(|wh| is_proxy_webhook(wh))
        .map(|x| {
            let token = x
                .token
                .as_ref()
                .map(|x| x.to_string())
                .ok_or_else(|| anyhow::anyhow!("webhook should contain token"));

            token.map(|token| CachedWebhook {
                id: x.id.get(),
                token,
            })
        })
        .transpose()
}

async fn create_pk_webhook(http: &Client, channel_id: u64) -> anyhow::Result<CachedWebhook> {
    let response = http
        .create_webhook(channel_id.try_into().unwrap(), WEBHOOK_NAME)?
        .exec()
        .await?;

    // todo: error handling here
    let val = response.model().await?;
    Ok(CachedWebhook {
        id: val.id.get(),
        token: val
            .token
            .ok_or_else(|| anyhow::anyhow!("webhook should contain token"))?,
    })
}

fn is_proxy_webhook(wh: &Webhook) -> bool {
    wh.kind == WebhookType::Incoming
        && wh.token.is_some()
        && wh.name.as_deref() == Some(WEBHOOK_NAME)
}

#[derive(Debug)]
pub struct WebhookExecuteRequest {
    pub channel_id: u64,
    pub username: String,
    pub avatar_url: Option<String>,
    pub content: Option<String>,
    pub embed: Option<Embed>,
}

#[derive(Debug)]
pub struct WebhookExecuteResult {
    pub message_id: u64,
}

pub async fn execute_webhook(
    http: &Client,
    req: &WebhookExecuteRequest,
) -> anyhow::Result<WebhookExecuteResult> {
    let webhook = get_webhook_cached(http, req.channel_id).await?;
    let mut request = http
        .execute_webhook(webhook.id.try_into().unwrap(), &webhook.token)
        .username(&req.username)?;

    if let Some(ref content) = req.content {
        request = request.content(content)?;
    }

    if let Some(ref avatar_url) = req.avatar_url {
        request = request.avatar_url(avatar_url);
    }

    let mut embeds = Vec::new();
    if let Some(ref embed) = req.embed {
        embeds.push(embed.clone());
        request = request.embeds(&embeds)?;
    }

    // todo: handle error if webhook was deleted, should invalidate and retry
    let result = request.wait().exec().await?;

    let model = result.model().await?;
    if model.channel_id != req.channel_id {
        // it's possible for someone to "redirect" a webhook to another channel
        // and the only way we find out is when we send a message.
        // if this has happened remove it from cache and refetch later
        WEBHOOK_CACHE.invalidate(&req.channel_id).await;
    }

    Ok(WebhookExecuteResult {
        message_id: model.id.get(),
    })
}
