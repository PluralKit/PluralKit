use hickory_client::{
    client::{AsyncClient, ClientHandle},
    rr::{DNSClass, Name, RData, RecordType},
    udp::UdpClientStream,
};
use reqwest::{StatusCode, redirect::Policy};
use std::{
    net::{Ipv4Addr, SocketAddr, SocketAddrV4},
    sync::Arc,
    time::Duration,
};
use tokio::{net::UdpSocket, sync::RwLock};
use tracing::{debug, error};

use axum::{Json, Router, extract::State, http::Uri, routing::post};

mod logger;

#[libpk::main]
async fn main() -> anyhow::Result<()> {
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

// it's still only on nightly .-.
// https://github.com/rust-lang/rust/issues/27709
pub const fn is_global_v4(ip: &Ipv4Addr) -> bool {
    !(ip.octets()[0] == 0 // "This network"
        || ip.is_private()
        || (ip.octets()[0] == 100 && (ip.octets()[1] & 0b1100_0000 == 0b0100_0000))
        || ip.is_loopback()
        || ip.is_link_local()
        // addresses reserved for future protocols (`192.0.0.0/24`)
        // .9 and .10 are documented as globally reachable so they're excluded
        || (
            ip.octets()[0] == 192 && ip.octets()[1] == 0 && ip.octets()[2] == 0
            && ip.octets()[3] != 9 && ip.octets()[3] != 10
        )
        || ip.is_documentation()
        || (ip.octets()[0] == 198 && (ip.octets()[1] & 0xfe) == 18)
        || (ip.octets()[0] & 240 == 240 && !ip.is_broadcast())
        || ip.is_broadcast())
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
            error!(?error, uri = req.url, "failed to parse uri");
            return DispatchResponse::BadData.to_string();
        }
        _ => {
            error!(uri = req.url, "uri is invalid");
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
    if ips.iter().any(|ip| !is_global_v4(&ip)) {
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
            return DispatchResponse::InvalidResponseCode(res.status()).to_string();
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
