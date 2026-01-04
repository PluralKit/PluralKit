use askama::Template;

use crate::auth::AuthState;
use crate::paddle::SubscriptionInfo;

macro_rules! render {
    ($stuff:expr) => {{
        let mut response = $stuff.render().unwrap().into_response();
        let headers = response.headers_mut();
        headers.insert(
            "content-type",
            axum::http::HeaderValue::from_static("text/html"),
        );
        response
    }};
}

pub(crate) use render;

pub fn message(message: String, session: Option<AuthState>) -> Index {
    Index {
        base_url: libpk::config.premium().base_url.clone(),
        session,
        show_login_form: false,
        message: Some(message),
        subscriptions: vec![],
        paddle: None,
    }
}

#[derive(Template)]
#[template(path = "index.html")]
pub struct Index {
    pub base_url: String,
    pub session: Option<AuthState>,
    pub show_login_form: bool,
    pub message: Option<String>,
    pub subscriptions: Vec<SubscriptionInfo>,
    pub paddle: Option<PaddleData>,
}

pub struct PaddleData {
    pub client_token: String,
    pub price_id: String,
    pub environment: String,
}

#[derive(Template)]
#[template(path = "cancel.html")]
pub struct Cancel {
    pub csrf_token: String,
    pub subscription: crate::paddle::SubscriptionInfo,
}
