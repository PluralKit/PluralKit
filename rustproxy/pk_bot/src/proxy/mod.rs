use self::{post_proxy::ProxyResult, webhook::WebhookExecuteRequest};
use crate::db::{self, MessageContext};
use sqlx::PgPool;
use thiserror::Error;
use twilight_http::Client;
use twilight_model::{
    channel::{message::MessageType, ChannelType, Message},
    guild::Permissions,
    user::User,
};

mod autoproxy;
mod post_proxy;
mod profile;
mod reply;
mod tags;
mod webhook;

#[derive(Error, Debug)]
// use this for pk;proxycheck or something
pub enum PreconditionFailure {
    #[error("bot is missing permissions (has: {has:?}, needs: {needs:?})")]
    BotMissingPermission {
        has: Permissions,
        needs: Permissions,
    },

    #[error("invalid channel type {0:?}")]
    InvalidChannelType(ChannelType),

    #[error("invalid message type {0:?}")]
    InvalidMessageType(MessageType),

    #[error("user is bot")]
    UserIsBot,

    #[error("user is webhook")]
    UserIsWebhook,

    #[error("user is discord system")]
    UserIsDiscordSystem,

    #[error("user has no system")]
    UserHasNoSystem,

    #[error("proxy disabled for system")]
    ProxyDisabledForSystem,

    #[error("proxy disabled in channel")]
    ProxyDisabledInChannel,

    #[error("message contains activity")]
    MessageContainsActivity,

    #[error("message contains sticker")]
    MessageContainsSticker,

    #[error("message is empty and has no attachments")]
    MessageIsEmpty,
}

// todo: the parameters here are nasty, refactor+put this code somewhere else maybe
pub fn check_preconditions(
    msg: &Message,
    channel_type: ChannelType,
    bot_permissions: Permissions,
    ctx: &MessageContext,
) -> Result<(), PreconditionFailure> {
    let required_permissions =
        Permissions::SEND_MESSAGES | Permissions::MANAGE_WEBHOOKS | Permissions::MANAGE_MESSAGES;
    if !bot_permissions.contains(required_permissions) {
        return Err(PreconditionFailure::BotMissingPermission {
            has: bot_permissions,
            needs: required_permissions,
        });
    }

    match channel_type {
        ChannelType::GuildText
        | ChannelType::GuildNews
        | ChannelType::GuildPrivateThread
        | ChannelType::GuildPublicThread
        | ChannelType::GuildNewsThread => Ok(()),
        wrong_type => Err(PreconditionFailure::InvalidChannelType(wrong_type)),
    }?;

    match msg.kind {
        MessageType::Regular | MessageType::Reply => Ok(()),
        wrong_type => Err(PreconditionFailure::InvalidMessageType(wrong_type)),
    }?;

    match msg {
        Message {
            author: User {
                system: Some(true), ..
            },
            ..
        } => Err(PreconditionFailure::UserIsDiscordSystem),
        Message {
            author: User { bot: true, .. },
            ..
        } => Err(PreconditionFailure::UserIsBot),
        Message {
            webhook_id: Some(_),
            ..
        } => Err(PreconditionFailure::UserIsWebhook),
        Message {
            activity: Some(_), ..
        } => Err(PreconditionFailure::MessageContainsActivity),
        Message {
            sticker_items: s, ..
        } if !s.is_empty() => Err(PreconditionFailure::MessageContainsSticker),
        Message {
            content: c,
            attachments: a,
            ..
        } if c.trim().is_empty() && a.is_empty() => Err(PreconditionFailure::MessageIsEmpty),
        _ => Ok(()),
    }?;

    match ctx {
        MessageContext {
            system_id: None, ..
        } => Err(PreconditionFailure::UserHasNoSystem),
        MessageContext {
            in_blacklist: Some(true),
            ..
        } => Err(PreconditionFailure::ProxyDisabledInChannel),
        MessageContext {
            proxy_enabled: Some(false),
            ..
        } => Err(PreconditionFailure::ProxyDisabledForSystem),
        _ => Ok(()),
    }?;

    Ok(())
}

struct ProxyMatchResult {
    member_id: i32,
    inner_content: String,
    _tags: Option<(String, String)>, // todo: need this for keepproxy
}

async fn match_tags_or_autoproxy(
    pool: &PgPool,
    msg: &Message,
    ctx: &MessageContext,
) -> anyhow::Result<Option<ProxyMatchResult>> {
    let guild_id = msg.guild_id.ok_or_else(|| anyhow::anyhow!("no guild id"))?;
    let system_id = ctx.system_id.ok_or_else(|| anyhow::anyhow!("no system"))?;

    let tags = db::get_proxy_tags(pool, system_id).await?;
    let ap_state = db::get_autoproxy_state(
        pool,
        system_id,
        guild_id.get() as i64,
        0, // all autoproxy has channel id 0? o.o
    )
    .await?;

    let tag_match = tags::match_proxy_tags(&tags, &msg.content);
    if let Some(tag_match) = tag_match {
        return Ok(Some(ProxyMatchResult {
            member_id: tag_match.member_id,
            inner_content: tag_match.inner_content,
            _tags: Some(tag_match.tags),
        }));
    }

    if let Some(ap_state) = ap_state {
        let res = autoproxy::resolve_autoproxy_member(ctx, &ap_state, &msg.content);
        if let Some(member_id) = res {
            return Ok(Some(ProxyMatchResult {
                inner_content: msg.content.clone(),
                member_id,
                _tags: None,
            }));
        }
    }

    Ok(None)
}

// todo: this shouldn't depend on a Message object (for reproxy/proxy command/etc)
pub async fn do_proxy(
    http: &Client,
    pool: &PgPool,
    msg: &Message,
    ctx: &MessageContext,
) -> anyhow::Result<()> {
    let guild_id = msg.guild_id.ok_or_else(|| anyhow::anyhow!("no guild id"))?;
    let system_id = ctx.system_id.ok_or_else(|| anyhow::anyhow!("no system"))?;

    // todo: unlatch check/exec should probably go in here somewhere

    let proxy_match = match_tags_or_autoproxy(pool, msg, ctx).await?;
    if let Some(result) = proxy_match {
        let profile =
            profile::fetch_proxy_profile(pool, guild_id.get(), system_id, result.member_id).await?;

        let webhook_req = WebhookExecuteRequest {
            channel_id: msg.channel_id.get(),
            avatar_url: profile.avatar_url().map(|s| s.to_string()),
            content: Some(result.inner_content.clone()),
            username: profile.formatted_name(),
            embed: msg
                .referenced_message
                .as_deref()
                .map(|msg| reply::create_reply_embed(guild_id, msg))
                .transpose()?,
        };

        let webhook_res = webhook::execute_webhook(http, &webhook_req).await?;

        let proxy_res = ProxyResult {
            channel_id: msg.channel_id.get(),
            guild_id: guild_id.get(),
            member_id: result.member_id,
            original_message_id: msg.id.get(),
            proxy_message_id: webhook_res.message_id,
            sender: msg.author.id.get(),
        };
        post_proxy::handle_post_proxy(http, pool, &proxy_res).await?;
    }
    Ok(())
}
