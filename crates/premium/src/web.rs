use askama::Template;

use crate::auth::AuthState;

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
        session: session,
        show_login_form: false,
        message: Some(message)
    }
}

#[derive(Template)]
#[template(path = "index.html")]
pub struct Index {
    pub session: Option<AuthState>,
    pub show_login_form: bool,
    pub message: Option<String>,
}
