use pk_macros::pk_model;

use chrono::NaiveDateTime;
use uuid::Uuid;

use crate::PrivacyLevel;

// todo: fix this
pub type SystemId = i32;

#[pk_model]
struct System {
    id: SystemId,
    #[json = "id"]
    #[private_patchable]
    hid: String,
    #[json = "uuid"]
    uuid: Uuid,
    #[json = "name"]
    #[privacy = name_privacy]
    name: Option<String>,
    #[json = "description"]
    #[privacy = description_privacy]
    description: Option<String>,
    #[json = "tag"]
    tag: Option<String>,
    #[json = "pronouns"]
    #[privacy = pronoun_privacy]
    pronouns: Option<String>,
    #[json = "avatar_url"]
    #[privacy = avatar_privacy]
    avatar_url: Option<String>,
    #[json = "banner"]
    #[privacy = banner_privacy]
    banner_image: Option<String>,
    #[json = "color"]
    color: Option<String>,
    token: Option<String>,
    webhook_url: Option<String>,
    webhook_token: Option<String>,
    #[json = "created"]
    created: NaiveDateTime,
    #[privacy]
    name_privacy: PrivacyLevel,
    #[privacy]
    avatar_privacy: PrivacyLevel,
    #[privacy]
    description_privacy: PrivacyLevel,
    #[privacy]
    banner_privacy: PrivacyLevel,
    #[privacy]
    member_list_privacy: PrivacyLevel,
    #[privacy]
    front_privacy: PrivacyLevel,
    #[privacy]
    front_history_privacy: PrivacyLevel,
    #[privacy]
    group_list_privacy: PrivacyLevel,
    #[privacy]
    pronoun_privacy: PrivacyLevel,
}
