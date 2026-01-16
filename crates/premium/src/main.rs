use askama::Template;
use axum::{
    Extension, Router,
    extract::State,
    response::{Html, IntoResponse, Response},
    routing::{get, post},
};
use tower_http::{catch_panic::CatchPanicLayer, services::ServeDir};
use tracing::info;

use api::{ApiContext, middleware};

mod auth;
mod error;
mod mailer;
mod paddle;
mod system;
mod web;

pub use api::fail;

async fn home_handler(
    State(ctx): State<ApiContext>,
    Extension(session): Extension<auth::AuthState>,
) -> Response {
    let subscriptions = match paddle::fetch_subscriptions_for_email(&ctx, &session.email).await {
        Ok(subs) => subs,
        Err(err) => {
            tracing::error!(?err, "failed to fetch subscriptions for {}", session.email);
            vec![]
        }
    };

    Html(
        web::Index {
            base_url: libpk::config.premium().base_url.clone(),
            session: Some(session),
            show_login_form: false,
            message: None,
            subscriptions,
            paddle: Some(web::PaddleData {
                client_token: libpk::config.premium().paddle_client_token.clone(),
                price_id: libpk::config.premium().paddle_price_id.clone(),
                environment: if libpk::config.premium().is_paddle_production {
                    "production"
                } else {
                    "sandbox"
                }
                .to_string(),
            }),
        }
        .render()
        .unwrap(),
    )
    .into_response()
}

// this function is manually formatted for easier legibility of route_services
#[rustfmt::skip]
fn router(ctx: ApiContext) -> Router {
    // processed upside down (???) so we have to put middleware at the end
    Router::new()
        .route("/", get(home_handler))
        .route("/info/", get(|| async { Html(include_str!("../templates/info.html")) }))

        .route("/login/{token}", get(|| async {
            "handled in auth middleware"
        }))
        .route("/login", post(|| async {
            "handled in auth middleware"
        }))
        .route("/logout", post(|| async {
            "handled in auth middleware"
        }))
        .route("/cancel", get(paddle::cancel_page).post(paddle::cancel))
        .route("/validate-token", post(system::validate_token))

        .layer(axum::middleware::from_fn_with_state(ctx.clone(), auth::middleware))

        .route("/paddle", post(paddle::webhook))

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
