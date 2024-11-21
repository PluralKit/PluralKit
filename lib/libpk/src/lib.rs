#![feature(let_chains)]
use metrics_exporter_prometheus::PrometheusBuilder;
use sentry::IntoDsn;
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt, EnvFilter};

pub mod db;
pub mod proto;
pub mod util;

pub mod _config;
pub use crate::_config::CONFIG as config;

// functions in this file are only used by the main function below

pub fn init_logging(component: &str) -> anyhow::Result<()> {
    if config.json_log {
        let mut layer = json_subscriber::layer();
        layer.inner_layer_mut().add_static_field(
            "component",
            serde_json::Value::String(component.to_string()),
        );
        tracing_subscriber::registry()
            .with(layer)
            .with(EnvFilter::from_default_env())
            .init();
    } else {
        tracing_subscriber::fmt()
            .with_env_filter(EnvFilter::from_default_env())
            .init();
    }

    Ok(())
}

pub fn init_metrics() -> anyhow::Result<()> {
    if config.run_metrics_server {
        // automatically spawns a http listener at :9000
        let builder = PrometheusBuilder::new();
        builder.install()?;
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
            libpk::init_logging($component)?;
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
