#![feature(ip)]

use hickory_client::{
    client::{AsyncClient, ClientHandle},
    rr::{DNSClass, Name, RData, RecordType},
    udp::UdpClientStream,
};
use reqwest::{redirect::Policy, StatusCode};
use std::{
    net::{Ipv4Addr, SocketAddr, SocketAddrV4},
    sync::Arc,
    time::Duration,
};
use tokio::{net::UdpSocket, sync::RwLock};
use tracing::{debug, error, info};
use tracing_subscriber::EnvFilter;

use axum::{extract::State, http::Uri, routing::post, Json, Router};

mod logger;

// this package does not currently use libpk

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    tracing_subscriber::fmt()
        .json()
        .with_env_filter(EnvFilter::from_default_env())
        .init();

    info!("hello world");

    let address = std::env::var("DNS_UPSTREAM").unwrap().parse().unwrap();
    let stream = UdpClientStream::<UdpSocket>::with_timeout(address, Duration::from_secs(3));
    let (client, bg) = AsyncClient::connect(stream).await?;
    tokio::spawn(bg);

    let app = Router::new()
        .route("/", post(dispatch))
        .with_state(Arc::new(RwLock::new(DNSClient(client))))
        .layer(axum::middleware::from_fn(logger::logger));

    let listener = tokio::net::TcpListener::bind("0.0.0.0:5000").await?;
    axum::serve(listener, app).await?;

    Ok(())
}

#[derive(Debug, serde::Deserialize)]
struct DispatchRequest {
    auth: String,
    url: String,
    payload: String,
    test: Option<String>,
}

#[allow(dead_code)]
#[derive(Debug)]
enum DispatchResponse {
    OK,
    BadData,
    ResolveFailed,
    NoIPs,
    InvalidIP,
    FetchFailed,
    InvalidResponseCode(StatusCode),
    TestFailed,
}

impl std::fmt::Display for DispatchResponse {
    fn fmt(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
        write!(f, "{:?}", self)
    }
}

async fn dispatch(
    // not entirely sure if this RwLock is the right way to do it
    State(dns): State<Arc<RwLock<DNSClient>>>,
    Json(req): Json<DispatchRequest>,
) -> String {
    // todo: fix
    if req.auth != std::env::var("HTTP_AUTH_TOKEN").unwrap() {
        panic!("bad auth");
    }

    let uri = match req.url.parse::<Uri>() {
        Ok(v) if v.scheme_str() == Some("https") && v.host().is_some() => v,
        Err(error) => {
            error!(?error, "failed to parse uri {}", req.url);
            return DispatchResponse::BadData.to_string();
        }
        _ => {
            error!("uri {} is invalid", req.url);
            return DispatchResponse::BadData.to_string();
        }
    };
    let ips = {
        let mut dns = dns.write().await;
        match dns.resolve(uri.host().unwrap().to_string()).await {
            Ok(v) => v,
            Err(error) => {
                error!(?error, "failed to resolve");
                return DispatchResponse::ResolveFailed.to_string();
            }
        }
    };
    if ips.iter().any(|ip| !ip.is_global()) {
        return DispatchResponse::InvalidIP.to_string();
    }

    if ips.len() == 0 {
        return DispatchResponse::NoIPs.to_string();
    }

    let ips: Vec<SocketAddr> = ips
        .iter()
        .map(|ip| SocketAddr::V4(SocketAddrV4::new(*ip, 443)))
        .collect();

    let client = reqwest::ClientBuilder::new()
        .user_agent("PluralKit Dispatch (https://pluralkit.me/api/dispatch/)")
        .redirect(Policy::none())
        .timeout(Duration::from_secs(10))
        .http1_only()
        .use_rustls_tls()
        .https_only(true)
        .resolve_to_addrs(uri.host().unwrap(), &ips)
        .build()
        .unwrap();

    let res = client
        .post(req.url.clone())
        .header("content-type", "application/json")
        .body(req.payload)
        .send()
        .await;

    match res {
        Ok(res) if res.status() != 200 => {
            return DispatchResponse::InvalidResponseCode(res.status()).to_string()
        }
        Err(error) => {
            error!(?error, url = req.url.clone(), "failed to fetch");
            return DispatchResponse::FetchFailed.to_string();
        }
        _ => {}
    }

    if let Some(test) = req.test {
        let test_res = client
            .post(req.url.clone())
            .header("content-type", "application/json")
            .body(test)
            .send()
            .await;

        match test_res {
            Ok(res) if res.status() != 401 => return DispatchResponse::TestFailed.to_string(),
            Err(error) => {
                error!(?error, url = req.url.clone(), "failed to fetch");
                return DispatchResponse::FetchFailed.to_string();
            }
            _ => {}
        }
    }

    DispatchResponse::OK.to_string()
}

struct DNSClient(AsyncClient);

impl DNSClient {
    async fn resolve(&mut self, host: String) -> anyhow::Result<Vec<Ipv4Addr>> {
        let resp = self
            .0
            .query(Name::from_ascii(host)?, DNSClass::IN, RecordType::A)
            .await?;

        debug!("got dns response: {resp:?}");

        Ok(resp
            .answers()
            .iter()
            .filter_map(|ans| {
                if let Some(RData::A(val)) = ans.data() {
                    Some(val.0)
                } else {
                    None
                }
            })
            .collect())
    }
}
