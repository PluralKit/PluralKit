use pk_macros::pk_model;

use chrono::NaiveDateTime;
use serde::{Deserialize, Serialize};
use serde_json::Value;
use uuid::Uuid;

use crate::{PrivacyLevel, SystemId, ValidationError};

// todo: fix
pub type MemberId = i32;

#[derive(Clone, Debug, Serialize, Deserialize, sqlx::Type)]
#[sqlx(type_name = "proxy_tag")]
pub struct ProxyTag {
    pub prefix: Option<String>,
    pub suffix: Option<String>,
}

#[pk_model]
struct Member {
    id: MemberId,
    #[json = "hid"]
    #[private_patchable]
    hid: String,
    #[json = "uuid"]
    uuid: Uuid,
    // TODO fix
    #[json = "system"]
    system: SystemId,

    #[json = "color"]
    #[patchable]
    color: Option<String>,
    #[json = "webhook_avatar_url"]
    #[patchable]
    webhook_avatar_url: Option<String>,
    #[json = "avatar_url"]
    #[patchable]
    avatar_url: Option<String>,
    #[json = "banner_image"]
    #[patchable]
    banner_image: Option<String>,
    #[json = "name"]
    #[privacy = name_privacy]
    #[patchable]
    name: String,
    #[json = "display_name"]
    #[patchable]
    display_name: Option<String>,
    #[json = "birthday"]
    #[patchable]
    birthday: Option<String>,
    #[json = "pronouns"]
    #[privacy = pronoun_privacy]
    #[patchable]
    pronouns: Option<String>,
    #[json = "description"]
    #[privacy = description_privacy]
    #[patchable]
    description: Option<String>,
    #[json = "proxy_tags"]
    // #[patchable]
    proxy_tags: Vec<ProxyTag>,
    #[json = "keep_proxy"]
    #[patchable]
    keep_proxy: bool,
    #[json = "tts"]
    #[patchable]
    tts: bool,
    #[json = "created"]
    created: NaiveDateTime,
    #[json = "message_count"]
    #[private_patchable]
    message_count: i32,
    #[json = "last_message_timestamp"]
    #[private_patchable]
    last_message_timestamp: Option<NaiveDateTime>,
    #[json = "allow_autoproxy"]
    #[patchable]
    allow_autoproxy: bool,

    #[privacy]
    #[json = "visibility"]
    member_visibility: PrivacyLevel,
    #[privacy]
    description_privacy: PrivacyLevel,
    #[privacy]
    banner_privacy: PrivacyLevel,
    #[privacy]
    avatar_privacy: PrivacyLevel,
    #[privacy]
    name_privacy: PrivacyLevel,
    #[privacy]
    birthday_privacy: PrivacyLevel,
    #[privacy]
    pronoun_privacy: PrivacyLevel,
    #[privacy]
    metadata_privacy: PrivacyLevel,
    #[privacy]
    proxy_privacy: PrivacyLevel,
}

impl<'de> Deserialize<'de> for PKMemberPatch {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        let mut patch: PKMemberPatch = Default::default();
        let value: Value = Value::deserialize(deserializer)?;

        if let Some(v) = value.get("name") {
            if let Some(name) = v.as_str() {
                patch.name = Some(name.to_string());
            } else if v.is_null() {
                patch.errors.push(ValidationError::simple(
                    "name",
                    "Member name cannot be set to null.",
                ));
            }
        }

        macro_rules! parse_string_simple {
            ($k:expr) => {
                match value.get($k) {
                    None => None,
                    Some(Value::Null) => Some(None),
                    Some(Value::String(s)) => Some(Some(s.clone())),
                    _ => {
                        patch.errors.push(ValidationError::new($k));
                        None
                    }
                }
            };
        }

        patch.color = parse_string_simple!("color").map(|v| v.map(|t| t.to_lowercase()));
        patch.display_name = parse_string_simple!("display_name");
        patch.avatar_url = parse_string_simple!("avatar_url");
        patch.banner_image = parse_string_simple!("banner");
        patch.birthday = parse_string_simple!("birthday"); // fix
        patch.pronouns = parse_string_simple!("pronouns");
        patch.description = parse_string_simple!("description");

        if let Some(keep_proxy) = value.get("keep_proxy").and_then(Value::as_bool) {
            patch.keep_proxy = Some(keep_proxy);
        }
        if let Some(tts) = value.get("tts").and_then(Value::as_bool) {
            patch.tts = Some(tts);
        }

        // todo: legacy import handling

        // todo: fix proxy_tag type in sea_query

        // if let Some(proxy_tags) = value.get("proxy_tags").and_then(Value::as_array) {
        //     patch.proxy_tags = Some(
        //         proxy_tags
        //             .iter()
        //             .filter_map(|tag| {
        //                 tag.as_object().map(|tag_obj| {
        //                     let prefix = tag_obj
        //                         .get("prefix")
        //                         .and_then(Value::as_str)
        //                         .map(|s| s.to_string());
        //                     let suffix = tag_obj
        //                         .get("suffix")
        //                         .and_then(Value::as_str)
        //                         .map(|s| s.to_string());
        //                     ProxyTag { prefix, suffix }
        //                 })
        //             })
        //             .collect(),
        //     )
        // }

        if let Some(privacy) = value.get("privacy").and_then(Value::as_object) {
            macro_rules! parse_privacy {
                ($v:expr) => {
                    match privacy.get($v) {
                        None => None,
                        Some(Value::Null) => Some(PrivacyLevel::Private),
                        Some(Value::String(s)) if s == "" || s == "private" => {
                            Some(PrivacyLevel::Private)
                        }
                        Some(Value::String(s)) if s == "public" => Some(PrivacyLevel::Public),
                        _ => {
                            patch.errors.push(ValidationError::new($v));
                            None
                        }
                    }
                };
            }

            patch.member_visibility = parse_privacy!("visibility");
            patch.name_privacy = parse_privacy!("name_privacy");
            patch.description_privacy = parse_privacy!("description_privacy");
            patch.banner_privacy = parse_privacy!("banner_privacy");
            patch.avatar_privacy = parse_privacy!("avatar_privacy");
            patch.birthday_privacy = parse_privacy!("birthday_privacy");
            patch.pronoun_privacy = parse_privacy!("pronoun_privacy");
            patch.proxy_privacy = parse_privacy!("proxy_privacy");
            patch.metadata_privacy = parse_privacy!("metadata_privacy");
        }

        Ok(patch)
    }
}
