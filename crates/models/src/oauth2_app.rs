use pk_macros::pk_model;
use uuid::Uuid;

#[pk_model]
struct ExternalApp {
    #[json = "id"]
    id: Uuid,
    #[json = "name"]
    #[patchable]
    name: String,
    #[json = "homepage_url"]
    #[patchable]
    homepage_url: String,

    #[private_patchable]
    oauth2_secret: Option<String>,
    #[json = "oauth2_allowed_redirects"]
    #[patchable]
    oauth2_allowed_redirects: Vec<String>,
    #[json = "oauth2_scopes"]
    #[patchable]
    oauth2_scopes: Vec<String>,

    #[private_patchable]
    api_rl_token: Option<String>,
    #[private_patchable]
    api_rl_rate: Option<i32>,
}
