use axum::{
    routing::{delete, get, patch, post},
    Router,
};
use tracing::info;

mod middleware;
mod util;

// this function is manually formatted for easier legibility of routes
#[rustfmt::skip]
#[tokio::main]
async fn main() -> anyhow::Result<()> {
    libpk::init_logging("api")?;
    libpk::init_metrics()?;
    info!("hello world");

    // processed upside down (???) so we have to put middleware at the end
    let app = Router::new()
        .route("/v2/systems/:system_id", get(util::rproxy))
        .route("/v2/systems/:system_id", patch(util::rproxy))
        .route("/v2/systems/:system_id/settings", get(util::rproxy))
        .route("/v2/systems/:system_id/settings", patch(util::rproxy))

        .route("/v2/systems/:system_id/members", get(util::rproxy))
        .route("/v2/members", post(util::rproxy))
        .route("/v2/members/:member_id", get(util::rproxy))
        .route("/v2/members/:member_id", patch(util::rproxy))
        .route("/v2/members/:member_id", delete(util::rproxy))

        .route("/v2/systems/:system_id/groups", get(util::rproxy))
        .route("/v2/groups", post(util::rproxy))
        .route("/v2/groups/:group_id", get(util::rproxy))
        .route("/v2/groups/:group_id", patch(util::rproxy))
        .route("/v2/groups/:group_id", delete(util::rproxy))

        .route("/v2/groups/:group_id/members", get(util::rproxy))
        .route("/v2/groups/:group_id/members/add", post(util::rproxy))
        .route("/v2/groups/:group_id/members/remove", post(util::rproxy))
        .route("/v2/groups/:group_id/members/overwrite", post(util::rproxy))

        .route("/v2/members/:member_id/groups", get(util::rproxy))
        .route("/v2/members/:member_id/groups/add", post(util::rproxy))
        .route("/v2/members/:member_id/groups/remove", post(util::rproxy))
        .route("/v2/members/:member_id/groups/overwrite", post(util::rproxy))

        .route("/v2/systems/:system_id/switches", get(util::rproxy))
        .route("/v2/systems/:system_id/switches", post(util::rproxy))
        .route("/v2/systems/:system_id/fronters", get(util::rproxy))

        .route("/v2/systems/:system_id/switches/:switch_id", get(util::rproxy))
        .route("/v2/systems/:system_id/switches/:switch_id", patch(util::rproxy))
        .route("/v2/systems/:system_id/switches/:switch_id/members", patch(util::rproxy))
        .route("/v2/systems/:system_id/switches/:switch_id", delete(util::rproxy))

        .route("/v2/systems/:system_id/guilds/:guild_id", get(util::rproxy))
        .route("/v2/systems/:system_id/guilds/:guild_id", patch(util::rproxy))

        .route("/v2/members/:member_id/guilds/:guild_id", get(util::rproxy))
        .route("/v2/members/:member_id/guilds/:guild_id", patch(util::rproxy))

        .route("/v2/systems/:system_id/autoproxy", get(util::rproxy))
        .route("/v2/systems/:system_id/autoproxy", patch(util::rproxy))
        .route("/v2/systems/:system_id/autoproxy/unlatch", post(util::rproxy))

        .route("/v2/messages/:message_id", get(util::rproxy))

        .route("/private/meta", get(util::rproxy))
        .route("/private/bulk_privacy/member", post(util::rproxy))
        .route("/private/bulk_privacy/group", post(util::rproxy))
        .route("/private/discord/callback", post(util::rproxy))

        .route("/v2/systems/:system_id/oembed.json", get(util::rproxy))
        .route("/v2/members/:member_id/oembed.json", get(util::rproxy))
        .route("/v2/groups/:group_id/oembed.json", get(util::rproxy))

        .layer(axum::middleware::from_fn(middleware::logger))
        .layer(middleware::ratelimit::ratelimiter(middleware::ratelimit::do_request_ratelimited)) // this sucks
        .layer(axum::middleware::from_fn(middleware::ignore_invalid_routes))
        .layer(axum::middleware::from_fn(middleware::cors))

        .route("/", get(|| async { axum::response::Redirect::to("https://pluralkit.me/api") }));

    let addr: &str = libpk::config.api.addr.as_ref();
    axum::Server::bind(&addr.parse()?)
        .serve(app.into_make_service())
        .await?;

    Ok(())
}
