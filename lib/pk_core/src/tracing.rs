use reqwest::blocking::{Client, ClientBuilder};

use tracing::*;
use tracing_subscriber::{fmt, layer::SubscriberExt, EnvFilter};
use tracing_appender::non_blocking::WorkerGuard;

use std::io::{Error, ErrorKind};

#[macro_export]
macro_rules! init {
    ($e:expr) => {
        let _guard1 = pk_core::tracing::init_inner(None, None, None).await;
        let _guard2 = span!(Level::ERROR, "service_name", service_name = $e).entered();
    };
    ($e:expr, $f:expr) => {
        let _guard1 = pk_core::tracing::init_inner($f, None, None).await;
        let _guard2 = span!(Level::ERROR, "service_name", service_name = $e).entered();
    };
    ($e:expr, $f:expr, $g:expr, $h:expr) => {
        let _guard1 = pk_core::tracing::init_inner($f, $g, $h).await;
        let _guard2 = span!(Level::ERROR, "service_name", service_name = $e).entered();
    };
}

pub use init;

pub async fn init_inner(url: Option<String>, user: Option<&str>, password: Option<&str>) -> Option<WorkerGuard> {
    let stdout_subscriber = tracing_subscriber::fmt().with_max_level(Level::INFO).finish();

    if url.is_none() {
        tracing::subscriber::set_global_default(stdout_subscriber)
            .expect("Unable to set a global collector");
        return None;
    }

    let client = tokio::task::spawn_blocking(|| {
        let client = ClientBuilder::new()
            .danger_accept_invalid_certs(true);
        client.build()
    }).await.unwrap().unwrap();

    let elastic = ElasticLogWriter {
        user: user.map(|v| v.to_string()),
        password: password.map(|v| v.to_string()),
        url: url.unwrap(),
        client
    };

    let (non_blocking, _guard) = tracing_appender::non_blocking::NonBlockingBuilder::default()
        .lossy(true)
        .buffered_lines_limit(2000)
        .finish(elastic);
    
    let subscriber = stdout_subscriber
        .with(EnvFilter::from_default_env().add_directive(tracing::Level::TRACE.into()))
        .with(
            fmt::Layer::new()
                    .json()
                    .flatten_event(true)
                    .with_writer(non_blocking)
        );

    tracing::subscriber::set_global_default(subscriber)
        .expect("Unable to set a global collector");

    return Some(_guard);
}

pub struct ElasticLogWriter {
    user: Option<String>,
    password: Option<String>,
    url: String,
    client: Client,
}

impl std::io::Write for ElasticLogWriter {
    fn write(&mut self, buf: &[u8]) -> std::io::Result<usize> {
        let data = String::from_utf8(buf.to_vec());
        if data.is_err() {
            return Err(Error::new(ErrorKind::Other, data.err().unwrap()));
        }

        let orig_json = data.unwrap();

        let cur_date = chrono::Utc::now().format("%Y-%m-%d");
        let mut builder = self.client.post(format!("{}/pluralkit-logs-{}/_doc", self.url.clone(), cur_date))
        .header("content-type", "application/json")
        .body(orig_json);
        
        if self.user.is_some() {
            builder = builder.basic_auth(self.user.as_ref().unwrap(), self.password.as_ref());
        }
        
        let res = builder.send();
        
        match res {
            Ok(_) => Ok(buf.len()),
            Err(err) => Err(Error::new(ErrorKind::Other, err)),
        }
    }

    fn flush(&mut self) -> std::io::Result<()> {
        Ok(())
    }
}
