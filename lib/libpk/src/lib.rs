use metrics_exporter_prometheus::PrometheusBuilder;
use tracing_subscriber::{EnvFilter, Registry};

pub mod db;
pub mod proto;
pub mod util;

pub mod _config;
pub use crate::_config::CONFIG as config;

pub fn init_logging(component: &str) -> anyhow::Result<()> {
    tracing_subscriber::fmt()
        .json()
        .with_env_filter(EnvFilter::from_default_env())
        .init();

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
