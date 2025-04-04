#![feature(let_chains)]
use std::net::SocketAddr;

use metrics_exporter_prometheus::PrometheusBuilder;
use sentry::IntoDsn;
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt, EnvFilter};

use sentry_tracing::event_from_event;

pub mod db;
pub mod runtime_config;
pub mod state;

pub mod _config;
pub use crate::_config::CONFIG as config;

// functions in this file are only used by the main function below

pub fn init_logging(component: &str) {
    let sentry_layer =
        sentry_tracing::layer().event_mapper(|md, ctx| match md.metadata().level() {
            &tracing::Level::ERROR => {
                // for some reason this works, but letting the library handle it doesn't
                let event = event_from_event(md, ctx);
                sentry::capture_event(event);
                sentry_tracing::EventMapping::Ignore
            }
            _ => sentry_tracing::EventMapping::Ignore,
        });

    if config.json_log {
        let mut layer = json_subscriber::layer();
        layer.inner_layer_mut().add_static_field(
            "component",
            serde_json::Value::String(component.to_string()),
        );
        tracing_subscriber::registry()
            .with(sentry_layer)
            .with(layer)
            .with(EnvFilter::from_default_env())
            .init();
    } else {
        tracing_subscriber::registry()
            .with(sentry_layer)
            .with(tracing_subscriber::fmt::layer())
            .with(EnvFilter::from_default_env())
            .init();
    }
}

pub fn init_metrics() -> anyhow::Result<()> {
    if config.run_metrics_server {
        PrometheusBuilder::new()
            .with_http_listener("[::]:9000".parse::<SocketAddr>().unwrap())
            .install()?;
    }
    Ok(())
}

pub fn init_sentry() -> sentry::ClientInitGuard {
    sentry::init(sentry::ClientOptions {
        dsn: config
            .sentry_url
            .clone()
            .map(|u| u.into_dsn().unwrap())
            .flatten(),
        release: sentry::release_name!(),
        ..Default::default()
    })
}

#[macro_export]
macro_rules! main {
    ($component:expr) => {
        fn main() -> anyhow::Result<()> {
            let _sentry_guard = libpk::init_sentry();
            // we might also be able to use env!("CARGO_CRATE_NAME") here
            libpk::init_logging($component);
            tokio::runtime::Builder::new_multi_thread()
                .enable_all()
                .build()
                .unwrap()
                .block_on(async {
                    if let Err(err) = libpk::init_metrics() {
                        tracing::error!("failed to init metrics collector: {err}");
                    };
                    tracing::info!("hello world");
                    if let Err(err) = real_main().await {
                        tracing::error!("failed to run service: {err}");
                    };
                });
            Ok(())
        }
    };
}
