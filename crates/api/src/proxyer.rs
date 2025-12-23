use crate::{
    auth::{AuthState, INTERNAL_APPID_HEADER, INTERNAL_SYSTEMID_HEADER},
    error::PKError,
};
use axum::{
    body::Body,
    extract::Request as ExtractRequest,
    http::Uri,
    response::{IntoResponse, Response},
};
use hyper_util::client::legacy::{Client, connect::HttpConnector};

#[derive(Clone)]
pub struct Proxyer {
    pub rproxy_uri: String,
    pub rproxy_client: Client<HttpConnector, Body>,
}

impl Proxyer {
    pub async fn rproxy(
        self,
        auth: AuthState,
        mut req: ExtractRequest<Body>,
    ) -> Result<Response, PKError> {
        let path = req.uri().path();
        let path_query = req
            .uri()
            .path_and_query()
            .map(|v| v.as_str())
            .unwrap_or(path);

        let uri = format!("{}{}", self.rproxy_uri, path_query);

        *req.uri_mut() = Uri::try_from(uri).unwrap();

        let headers = req.headers_mut();

        headers.remove(INTERNAL_SYSTEMID_HEADER);
        headers.remove(INTERNAL_APPID_HEADER);

        if let Some(sid) = auth.system_id() {
            headers.append(INTERNAL_SYSTEMID_HEADER, sid.into());
        }

        if let Some(aid) = auth.app_id() {
            headers.append(INTERNAL_APPID_HEADER, aid.into());
        }

        Ok(self.rproxy_client.request(req).await?.into_response())
    }
}
