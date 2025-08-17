use pk_macros::pk_model;

use chrono::{DateTime, Utc, NaiveDateTime};
use uuid::Uuid;
use base64::{prelude::BASE64_STANDARD, Engine};
use jsonwebtoken::{
    crypto::{sign, verify},
    DecodingKey, EncodingKey,
};

use crate::SystemId;

#[derive(sqlx::Type, Debug, Clone, PartialEq, serde::Serialize)]
#[serde(rename_all = "snake_case")]
#[sqlx(rename_all = "snake_case")]
#[sqlx(type_name = "api_key_type")]
pub enum ApiKeyType {
    Dashboard,
    UserCreated,
    ExternalApp,
}

#[pk_model]
struct ApiKey {
    #[json = "id"]
    id: Uuid,
    system: SystemId,
    #[json = "type"]
    kind: ApiKeyType,
    #[json = "scopes"]
    scopes: Vec<String>,
    #[json = "app"]
    app: Option<Uuid>,
    #[json = "name"]
    #[patchable]
    name: Option<String>,

    #[json = "discord_id"]
    discord_id: Option<i64>,
    #[private_patchable]
    discord_access_token: Option<String>,
    #[private_patchable]
    discord_refresh_token: Option<String>,
    #[private_patchable]
    discord_expires_at: Option<NaiveDateTime>,

    #[json = "created"]
    created: DateTime<Utc>,
}

const SIGNATURE_ALGORITHM: jsonwebtoken::Algorithm = jsonwebtoken::Algorithm::ES256;

impl PKApiKey {
    pub fn to_header_str(self, system_uuid: Uuid, key: &EncodingKey) -> String {
        let b64 = BASE64_STANDARD.encode(
            serde_json::to_vec(&serde_json::json!({
                "tid": self.id.to_string(),
                "sid": system_uuid.to_string(),
                "type": self.kind,
                "scopes": self.scopes,
            }))
            .expect("should not fail"),
        );

        let signature = sign(b64.as_bytes(), key, SIGNATURE_ALGORITHM).expect("should not fail");

        format!("pkapi:{b64}:{signature}")
    }

    /// Parse a header string into a token uuid
    pub fn parse_header_str(token: String, key: &DecodingKey) -> Option<Uuid> {
        let mut parts = token.split(":");
        let pkapi = parts.next();
        if pkapi.is_none_or(|v| v != "pkapi") {
            return None;
        }

        let Some(jsonblob) = parts.next() else {
            return None;
        };
        let Some(sig) = parts.next() else {
            return None;
        };

        // verify signature before doing anything else
        let valid = verify(sig, jsonblob.as_bytes(), key, SIGNATURE_ALGORITHM);
        if valid.is_err() || matches!(valid, Ok(false)) {
            return None;
        }

        let Ok(bytes) = BASE64_STANDARD.decode(jsonblob) else {
            return None;
        };

        let Ok(obj) = serde_json::from_slice::<serde_json::Value>(bytes.as_slice()) else {
            return None;
        };

        obj.get("tid")
            .map(|v| v.as_str().map(|f| Uuid::parse_str(f).ok()))
            .flatten()
            .flatten()
    }
}
