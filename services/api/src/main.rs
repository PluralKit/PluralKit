use axum::{
    body::Body,
    extract::{Request as ExtractRequest, State},
    http::{Response, StatusCode, Uri},
    response::IntoResponse,
    routing::{delete, get, patch, post},
    Router,
};
use hyper_util::{
    client::legacy::{connect::HttpConnector, Client},
    rt::TokioExecutor,
};
use tracing::{error, info};

mod endpoints;
mod error;
mod middleware;
mod util;

#[derive(Clone)]
pub struct ApiContext {
    pub db: sqlx::postgres::PgPool,
    pub redis: fred::pool::RedisPool,

    rproxy_uri: String,
    rproxy_client: Client<HttpConnector, Body>,
}

async fn rproxy(
    State(ctx): State<ApiContext>,
    mut req: ExtractRequest<Body>,
) -> Result<Response<Body>, StatusCode> {
    let path = req.uri().path();
    let path_query = req
        .uri()
        .path_and_query()
        .map(|v| v.as_str())
        .unwrap_or(path);

    let uri = format!("{}{}", ctx.rproxy_uri, path_query);

    *req.uri_mut() = Uri::try_from(uri).unwrap();

    Ok(ctx
        .rproxy_client
        .request(req)
        .await
        .map_err(|err| {
            error!("failed to serve reverse proxy to dotnet-api: {:?}", err);
            StatusCode::BAD_GATEWAY
        })?
        .into_response())
}

// this function is manually formatted for easier legibility of route_services
#[rustfmt::skip]
#[tokio::main]
async fn main() -> anyhow::Result<()> {
    libpk::init_logging("api")?;
    libpk::init_metrics()?;
    info!("hello world");

    let db = libpk::db::init_data_db().await?;
    let redis = libpk::db::init_redis().await?;

    let rproxy_uri = Uri::from_static(&libpk::config.api.remote_url).to_string();
    let rproxy_client = hyper_util::client::legacy::Client::<(), ()>::builder(TokioExecutor::new())
            .build(HttpConnector::new());

    let ctx = ApiContext {
        db,
        redis,

        rproxy_uri: rproxy_uri[..rproxy_uri.len() - 1].to_string(),
        rproxy_client,
     };

    // processed upside down (???) so we have to put middleware at the end
    let app = Router::new()
        .route("/v2/systems/:system_id", get(rproxy))
        .route("/v2/systems/:system_id", patch(rproxy))
        .route("/v2/systems/:system_id/settings", get(rproxy))
        .route("/v2/systems/:system_id/settings", patch(rproxy))

        .route("/v2/systems/:system_id/members", get(rproxy))
        .route("/v2/members", post(rproxy))
        .route("/v2/members/:member_id", get(rproxy))
        .route("/v2/members/:member_id", patch(rproxy))
        .route("/v2/members/:member_id", delete(rproxy))

        .route("/v2/systems/:system_id/groups", get(rproxy))
        .route("/v2/groups", post(rproxy))
        .route("/v2/groups/:group_id", get(rproxy))
        .route("/v2/groups/:group_id", patch(rproxy))
        .route("/v2/groups/:group_id", delete(rproxy))

        .route("/v2/groups/:group_id/members", get(rproxy))
        .route("/v2/groups/:group_id/members/add", post(rproxy))
        .route("/v2/groups/:group_id/members/remove", post(rproxy))
        .route("/v2/groups/:group_id/members/overwrite", post(rproxy))

        .route("/v2/members/:member_id/groups", get(rproxy))
        .route("/v2/members/:member_id/groups/add", post(rproxy))
        .route("/v2/members/:member_id/groups/remove", post(rproxy))
        .route("/v2/members/:member_id/groups/overwrite", post(rproxy))

        .route("/v2/systems/:system_id/switches", get(rproxy))
        .route("/v2/systems/:system_id/switches", post(rproxy))
        .route("/v2/systems/:system_id/fronters", get(rproxy))

        .route("/v2/systems/:system_id/switches/:switch_id", get(rproxy))
        .route("/v2/systems/:system_id/switches/:switch_id", patch(rproxy))
        .route("/v2/systems/:system_id/switches/:switch_id/members", patch(rproxy))
        .route("/v2/systems/:system_id/switches/:switch_id", delete(rproxy))

        .route("/v2/systems/:system_id/guilds/:guild_id", get(rproxy))
        .route("/v2/systems/:system_id/guilds/:guild_id", patch(rproxy))

        .route("/v2/members/:member_id/guilds/:guild_id", get(rproxy))
        .route("/v2/members/:member_id/guilds/:guild_id", patch(rproxy))

        .route("/v2/systems/:system_id/autoproxy", get(rproxy))
        .route("/v2/systems/:system_id/autoproxy", patch(rproxy))

        .route("/v2/messages/:message_id", get(rproxy))

        .route("/private/meta", get(endpoints::private::meta))
        .route("/private/bulk_privacy/member", post(rproxy))
        .route("/private/bulk_privacy/group", post(rproxy))
        .route("/private/discord/callback", post(rproxy))

        .route("/v2/systems/:system_id/oembed.json", get(rproxy))
        .route("/v2/members/:member_id/oembed.json", get(rproxy))
        .route("/v2/groups/:group_id/oembed.json", get(rproxy))

        .layer(axum::middleware::from_fn(middleware::logger))
        .layer(middleware::ratelimit::ratelimiter(middleware::ratelimit::do_request_ratelimited)) // this sucks
        .layer(axum::middleware::from_fn(middleware::ignore_invalid_routes))
        .layer(axum::middleware::from_fn(middleware::cors))

        .layer(tower_http::catch_panic::CatchPanicLayer::custom(util::handle_panic))

        .with_state(ctx)

        .route("/", get(|| async { axum::response::Redirect::to("https://pluralkit.me/api") }));

    let addr: &str = libpk::config.api.addr.as_ref();
    let listener = tokio::net::TcpListener::bind(addr).await?;
    axum::serve(listener, app).await?;

    Ok(())
}
