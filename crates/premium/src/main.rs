use askama::Template;
use axum::{
    Extension, Router,
    response::Html,
    routing::{get, post},
};
use tower_http::{catch_panic::CatchPanicLayer, services::ServeDir};
use tracing::info;

use api::{ApiContext, middleware};

mod auth;
mod mailer;
mod web;

// this function is manually formatted for easier legibility of route_services
#[rustfmt::skip]
fn router(ctx: ApiContext) -> Router {
    // processed upside down (???) so we have to put middleware at the end
    Router::new()
        .route("/", get(|Extension(session): Extension<auth::AuthState>| async move {
            Html(web::Index {
                session: Some(session),
                show_login_form: false,
                message: None,
            }.render().unwrap())
        }))

        .route("/login/{token}", get(|| async {
            "handled in auth middleware"
        }))
        .route("/login", post(|| async {
            "handled in auth middleware"
        }))
        .route("/logout", post(|| async {
            "handled in auth middleware"
        }))

        .layer(axum::middleware::from_fn_with_state(ctx.clone(), auth::middleware))
        .layer(axum::middleware::from_fn(middleware::logger::logger))
        .nest_service("/static", ServeDir::new("static"))
        .layer(CatchPanicLayer::custom(api::util::handle_panic))

        .with_state(ctx)
}

#[libpk::main]
async fn main() -> anyhow::Result<()> {
    let db = libpk::db::init_data_db().await?;
    let redis = libpk::db::init_redis().await?;

    let ctx = ApiContext { db, redis };

    let app = router(ctx);

    let addr: &str = libpk::config.api().addr.as_ref();

    let listener = tokio::net::TcpListener::bind(addr).await?;
    info!("listening on {}", addr);
    axum::serve(listener, app).await?;

    Ok(())
}
