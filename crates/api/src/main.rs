use api::ApiContext;
use auth::AuthState;
use axum::{
    Extension, Router,
    body::Body,
    extract::Request as ExtractRequest,
    http::Uri,
    routing::{delete, get, patch, post},
};
use hyper_util::{client::legacy::connect::HttpConnector, rt::TokioExecutor};
use libpk::config;
use tracing::{info, warn};

use crate::proxyer::Proxyer;

mod auth;
mod endpoints;
mod error;
mod middleware;
mod proxyer;
mod util;

// this function is manually formatted for easier legibility of route_services
#[rustfmt::skip]
fn router(ctx: ApiContext, proxyer: Proxyer) -> Router {
    let rproxy = |Extension(auth): Extension<AuthState>, req: ExtractRequest<Body>| {
        proxyer.rproxy(auth, req)
    };

    // processed upside down (???) so we have to put middleware at the end
    Router::new()
        .route("/v2/systems/{system_id}", get(rproxy.clone()))
        .route("/v2/systems/{system_id}", patch(rproxy.clone()))
        .route("/v2/systems/{system_id}/settings", get(endpoints::system::get_system_settings))
        .route("/v2/systems/{system_id}/settings", patch(rproxy.clone()))

        .route("/v2/systems/{system_id}/members", get(rproxy.clone()))
        .route("/v2/members", post(rproxy.clone()))
        .route("/v2/members/{member_id}", get(rproxy.clone()))
        .route("/v2/members/{member_id}", patch(rproxy.clone()))
        .route("/v2/members/{member_id}", delete(rproxy.clone()))

        .route("/v2/systems/{system_id}/groups", get(rproxy.clone()))
        .route("/v2/groups", post(rproxy.clone()))
        .route("/v2/groups/{group_id}", get(rproxy.clone()))
        .route("/v2/groups/{group_id}", patch(rproxy.clone()))
        .route("/v2/groups/{group_id}", delete(rproxy.clone()))

        .route("/v2/groups/{group_id}/members", get(rproxy.clone()))
        .route("/v2/groups/{group_id}/members/add", post(rproxy.clone()))
        .route("/v2/groups/{group_id}/members/remove", post(rproxy.clone()))
        .route("/v2/groups/{group_id}/members/overwrite", post(rproxy.clone()))

        .route("/v2/members/{member_id}/groups", get(rproxy.clone()))
        .route("/v2/members/{member_id}/groups/add", post(rproxy.clone()))
        .route("/v2/members/{member_id}/groups/remove", post(rproxy.clone()))
        .route("/v2/members/{member_id}/groups/overwrite", post(rproxy.clone()))

        .route("/v2/systems/{system_id}/switches", get(rproxy.clone()))
        .route("/v2/systems/{system_id}/switches", post(rproxy.clone()))
        .route("/v2/systems/{system_id}/fronters", get(rproxy.clone()))

        .route("/v2/systems/{system_id}/switches/{switch_id}", get(rproxy.clone()))
        .route("/v2/systems/{system_id}/switches/{switch_id}", patch(rproxy.clone()))
        .route("/v2/systems/{system_id}/switches/{switch_id}/members", patch(rproxy.clone()))
        .route("/v2/systems/{system_id}/switches/{switch_id}", delete(rproxy.clone()))

        .route("/v2/systems/{system_id}/guilds/{guild_id}", get(rproxy.clone()))
        .route("/v2/systems/{system_id}/guilds/{guild_id}", patch(rproxy.clone()))

        .route("/v2/members/{member_id}/guilds/{guild_id}", get(rproxy.clone()))
        .route("/v2/members/{member_id}/guilds/{guild_id}", patch(rproxy.clone()))

        .route("/v2/systems/{system_id}/autoproxy", get(rproxy.clone()))
        .route("/v2/systems/{system_id}/autoproxy", patch(rproxy.clone()))

        .route("/v2/messages/{message_id}", get(rproxy.clone()))

        .route("/v2/bulk", post(endpoints::bulk::bulk))

        .route("/private/bulk_privacy/member", post(rproxy.clone()))
        .route("/private/bulk_privacy/group", post(rproxy.clone()))
        .route("/private/discord/callback", post(rproxy.clone()))
        .route("/private/discord/callback2", post(endpoints::private::discord_callback))
        .route("/private/discord/shard_state", get(endpoints::private::discord_state))
        .route("/private/dash_views", post(endpoints::private::dash_views))
        .route("/private/dash_view/{id}", get(endpoints::private::dash_view))
        .route("/private/stats", get(endpoints::private::meta))

        .route("/v2/systems/{system_id}/oembed.json", get(rproxy.clone()))
        .route("/v2/members/{member_id}/oembed.json", get(rproxy.clone()))
        .route("/v2/groups/{group_id}/oembed.json", get(rproxy.clone()))

        .layer(axum::middleware::from_fn_with_state(
            if config.api().use_ratelimiter {
                Some(ctx.redis.clone())
            } else {
                warn!("running without request rate limiting!");
                None
            },
            middleware::ratelimit::do_request_ratelimited)
        )
        .layer(axum::middleware::from_fn(middleware::ignore_invalid_routes::ignore_invalid_routes))
        .layer(axum::middleware::from_fn_with_state(ctx.clone(), middleware::params::params))
        .layer(axum::middleware::from_fn_with_state(ctx.clone(), middleware::auth::auth))
        .layer(axum::middleware::from_fn(middleware::logger::logger))
        .layer(axum::middleware::from_fn(middleware::cors::cors))
        .layer(tower_http::catch_panic::CatchPanicLayer::custom(util::handle_panic))

        .with_state(ctx)

        .route("/", get(|| async { axum::response::Redirect::to("https://pluralkit.me/api") }))
}

#[libpk::main]
async fn main() -> anyhow::Result<()> {
    let db = libpk::db::init_data_db().await?;
    let redis = libpk::db::init_redis().await?;

    let rproxy_uri = Uri::from_static(&libpk::config.api().remote_url).to_string();
    let rproxy_client = hyper_util::client::legacy::Client::<(), ()>::builder(TokioExecutor::new())
        .build(HttpConnector::new());

    let proxyer = Proxyer {
        rproxy_uri: rproxy_uri[..rproxy_uri.len() - 1].to_string(),
        rproxy_client,
    };

    let ctx = ApiContext { db, redis };

    let app = router(ctx, proxyer);

    let addr: &str = libpk::config.api().addr.as_ref();

    let listener = tokio::net::TcpListener::bind(addr).await?;
    info!("listening on {}", addr);
    axum::serve(listener, app).await?;

    Ok(())
}
