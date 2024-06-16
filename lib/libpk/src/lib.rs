use gethostname::gethostname;
use metrics_exporter_prometheus::PrometheusBuilder;
use tracing_subscriber::{prelude::__tracing_subscriber_SubscriberExt, EnvFilter, Registry};

pub mod db;
pub mod proto;

pub mod _config;
pub use crate::_config::CONFIG as config;

pub fn init_logging(component: &str) -> anyhow::Result<()> {
    let subscriber = Registry::default()
        .with(EnvFilter::from_default_env())
        .with(tracing_subscriber::fmt::layer());

    if let Some(gelf_url) = &config.gelf_log_url {
        let gelf_logger = tracing_gelf::Logger::builder()
            .additional_field("component", component)
            .additional_field("hostname", gethostname().to_str());
        let mut conn_handle = gelf_logger
            .init_udp_with_subscriber(gelf_url, subscriber)
            .unwrap();
        tokio::spawn(async move { conn_handle.connect().await });
    } else {
        // gelf_logger internally sets the global subscriber
        tracing::subscriber::set_global_default(subscriber)
            .expect("unable to set global subscriber");
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
