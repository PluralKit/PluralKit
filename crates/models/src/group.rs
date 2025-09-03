use pk_macros::pk_model;

use chrono::{DateTime, Utc};
use serde::Deserialize;
use serde_json::Value;
use uuid::Uuid;

use crate::{PrivacyLevel, SystemId, ValidationError};

// todo: fix
pub type GroupId = i32;

#[pk_model]
struct Group {
    id: GroupId,
    #[json = "hid"]
    #[private_patchable]
    hid: String,
    #[json = "uuid"]
    uuid: Uuid,
    // TODO fix
    #[json = "system"]
    system: SystemId,

    #[json = "name"]
    #[privacy = name_privacy]
    #[patchable]
    name: String,
    #[json = "display_name"]
    #[patchable]
    display_name: Option<String>,
    #[json = "color"]
    #[patchable]
    color: Option<String>,
    #[json = "icon"]
    #[patchable]
    icon: Option<String>,
    #[json = "banner_image"]
    #[patchable]
    banner_image: Option<String>,
    #[json = "description"]
    #[privacy = description_privacy]
    #[patchable]
    description: Option<String>,
    #[json = "created"]
    created: DateTime<Utc>,

    #[privacy]
    name_privacy: PrivacyLevel,
    #[privacy]
    description_privacy: PrivacyLevel,
    #[privacy]
    banner_privacy: PrivacyLevel,
    #[privacy]
    icon_privacy: PrivacyLevel,
    #[privacy]
    list_privacy: PrivacyLevel,
    #[privacy]
    metadata_privacy: PrivacyLevel,
    #[privacy]
    visibility: PrivacyLevel,
}

impl<'de> Deserialize<'de> for PKGroupPatch {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        let mut patch: PKGroupPatch = Default::default();
        let value: Value = Value::deserialize(deserializer)?;

        if let Some(v) = value.get("name") {
            if let Some(name) = v.as_str() {
                patch.name = Some(name.to_string());
            } else if v.is_null() {
                patch.errors.push(ValidationError::simple(
                    "name",
                    "Group name cannot be set to null.",
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

        patch.display_name = parse_string_simple!("display_name");
        patch.description = parse_string_simple!("description");
        patch.icon = parse_string_simple!("icon");
        patch.banner_image = parse_string_simple!("banner");
        patch.color = parse_string_simple!("color").map(|v| v.map(|t| t.to_lowercase()));

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

            patch.name_privacy = parse_privacy!("name_privacy");
            patch.description_privacy = parse_privacy!("description_privacy");
            patch.banner_privacy = parse_privacy!("banner_privacy");
            patch.icon_privacy = parse_privacy!("icon_privacy");
            patch.list_privacy = parse_privacy!("list_privacy");
            patch.metadata_privacy = parse_privacy!("metadata_privacy");
            patch.visibility = parse_privacy!("visibility");
        }

        Ok(patch)
    }
}
