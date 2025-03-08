use std::{str::FromStr, sync::Arc};

use crate::PKAvatarError;
use anyhow::Context;
use reqwest::{Client, StatusCode, Url};
use std::error::Error;
use std::fmt::Write;
use std::time::Instant;
use tracing::{error, instrument};

const MAX_SIZE: u64 = 8 * 1024 * 1024;

#[allow(dead_code)]
pub struct PullResult {
    pub data: Vec<u8>,
    pub content_type: String,
    pub last_modified: Option<String>,
}

#[instrument(skip_all)]
pub async fn pull(
    client: Arc<Client>,
    parsed_url: &ParsedUrl,
) -> Result<PullResult, PKAvatarError> {
    let time_before = Instant::now();
    let mut trimmed_url = trim_url_query(&parsed_url.full_url)?;
    if trimmed_url.host_str() == Some("media.discordapp.net") {
        trimmed_url
            .set_host(Some("cdn.discordapp.com"))
            .expect("set_host should not fail");
    }
    let response = client.get(trimmed_url.clone()).send().await.map_err(|e| {
        // terrible
        let mut s = format!("{}", e);
        if let Some(src) = e.source() {
            let _ = write!(s, ": {}", src);
            let mut err = src;
            while let Some(src) = err.source() {
                let _ = write!(s, ": {}", src);
                err = src;
            }
        }

        error!("network error for {}: {}", parsed_url.full_url, s);
        PKAvatarError::NetworkErrorString(s)
    })?;
    let time_after_headers = Instant::now();
    let status = response.status();

    if status != StatusCode::OK {
        if trimmed_url.host_str() == Some("cdn.discordapp.com") {
            return Err(PKAvatarError::BadCdnResponse(status));
        } else {
            return Err(PKAvatarError::BadServerResponse(status));
        }
    }

    let size = match response.content_length() {
        None => return Err(PKAvatarError::MissingHeader("Content-Length")),
        Some(size) if size > MAX_SIZE => {
            return Err(PKAvatarError::ImageFileSizeTooLarge(size, MAX_SIZE))
        }
        Some(size) => size,
    };

    let content_type = response
        .headers()
        .get(reqwest::header::CONTENT_TYPE)
        .and_then(|x| x.to_str().ok()) // invalid (non-unicode) header = missing, why not
        .map(|mime| mime.split(';').next().unwrap_or("")) // cut off at ;
        .ok_or(PKAvatarError::MissingHeader("Content-Type"))?
        .to_owned();
    let mime = match content_type.as_str() {
        mime @ ("image/jpeg" | "image/png" | "image/gif" | "image/webp" | "image/tiff") => mime,
        _ => return Err(PKAvatarError::UnsupportedContentType(content_type)),
    };

    let last_modified = response
        .headers()
        .get(reqwest::header::LAST_MODIFIED)
        .and_then(|x| x.to_str().ok())
        .map(|x| x.to_string());

    let body = response.bytes().await.map_err(|e| {
        error!("network error for {}: {}", parsed_url.full_url, e);
        PKAvatarError::NetworkError(e)
    })?;
    if body.len() != size as usize {
        // ???does this ever happen?
        return Err(PKAvatarError::InternalError(anyhow::anyhow!(
            "server responded with wrong length"
        )));
    }
    let time_after_body = Instant::now();

    let headers_time = time_after_headers - time_before;
    let body_time = time_after_body - time_after_headers;

    // can't do dynamic log level lmao
    if status != StatusCode::OK {
        tracing::warn!(
            "{}: {} (headers: {}ms, body: {}ms)",
            status,
            &trimmed_url,
            headers_time.as_millis(),
            body_time.as_millis()
        );
    } else {
        tracing::info!(
            "{}: {} (headers: {}ms, body: {}ms)",
            status,
            &trimmed_url,
            headers_time.as_millis(),
            body_time.as_millis()
        );
    };

    Ok(PullResult {
        data: body.to_vec(),
        content_type: mime.to_string(),
        last_modified,
    })
}

#[allow(dead_code)]
#[derive(Debug)]
pub struct ParsedUrl {
    pub channel_id: u64,
    pub attachment_id: u64,
    pub filename: String,
    pub full_url: String,
}

pub fn parse_url(url: &str) -> anyhow::Result<ParsedUrl> {
    // todo: should this return PKAvatarError::InvalidCdnUrl?
    let url = Url::from_str(url).context("invalid url")?;

    match (url.scheme(), url.domain()) {
        ("https", Some("media.discordapp.net" | "cdn.discordapp.com")) => {}
        _ => anyhow::bail!("not a discord cdn url"),
    }

    match url
        .path_segments()
        .map(|x| x.collect::<Vec<_>>())
        .as_deref()
    {
        Some([_, channel_id, attachment_id, filename]) => {
            let channel_id = u64::from_str(channel_id).context("invalid channel id")?;
            let attachment_id = u64::from_str(attachment_id).context("invalid channel id")?;

            Ok(ParsedUrl {
                channel_id,
                attachment_id,
                filename: filename.to_string(),
                full_url: url.to_string(),
            })
        }
        _ => anyhow::bail!("invaild discord cdn url"),
    }
}

fn trim_url_query(url: &str) -> anyhow::Result<Url> {
    let mut parsed = Url::parse(url)?;

    let mut qs = form_urlencoded::Serializer::new(String::new());
    for (key, value) in parsed.query_pairs() {
        match key.as_ref() {
            "ex" | "is" | "hm" => {
                qs.append_pair(key.as_ref(), value.as_ref());
            }
            _ => {}
        }
    }

    let new_query = qs.finish();
    parsed.set_query(if new_query.len() > 0 {
        Some(&new_query)
    } else {
        None
    });

    Ok(parsed)
}
