use std::net::IpAddr;

use serde::{Deserialize, Serialize};
use sqlx::{
    FromRow,
    types::chrono::{DateTime, Utc},
};
use uuid::Uuid;

#[derive(FromRow, Serialize)]
pub struct ImageData {
    pub hash: String,
    pub url: String,
    pub file_size: i32,
    pub width: i32,
    pub height: i32,
    pub content_type: String,
    pub created_at: Option<DateTime<Utc>>,
}

#[derive(FromRow, Serialize)]
pub struct ImageMeta {
    pub id: Uuid,
    #[serde(skip_serializing)]
    pub system_uuid: Uuid,
    #[serde(skip_serializing)]
    pub image: String,
    pub proxy_image: Option<String>,
    pub kind: ImageKind,

    #[serde(skip_serializing)]
    pub original_url: Option<String>,
    #[serde(skip_serializing)]
    pub original_file_size: Option<i32>,
    #[serde(skip_serializing)]
    pub original_type: Option<String>,
    #[serde(skip_serializing)]
    pub original_attachment_id: Option<i64>,

    pub uploaded_by_account: Option<i64>,
    pub uploaded_by_ip: Option<IpAddr>,
    pub uploaded_at: Option<DateTime<Utc>>,
}

#[derive(FromRow, Serialize)]
pub struct Image {
    #[sqlx(flatten)]
    pub meta: ImageMeta,
    #[sqlx(flatten)]
    pub data: ImageData,
}

pub struct ImageResult {
    pub is_new: bool,
    pub uuid: Uuid,
}

#[derive(FromRow, Serialize)]
pub struct Stats {
    pub total_images: i64,
    pub total_file_size: i64,
}

#[derive(Serialize, Deserialize, Clone, Copy, Debug, sqlx::Type, PartialEq)]
#[serde(rename_all = "snake_case")]
#[sqlx(rename_all = "snake_case", type_name = "text")]
pub enum ImageKind {
    Avatar,
    Banner,
    PremiumAvatar,
    PremiumBanner,
}

impl ImageKind {
    pub fn size(&self) -> (u32, u32) {
        match self {
            Self::Avatar => (512, 512),
            Self::Banner => (1024, 1024),
            Self::PremiumAvatar => (0, 0),
            Self::PremiumBanner => (0, 0),
        }
    }
    pub fn is_premium(&self) -> bool {
        matches!(self, ImageKind::PremiumAvatar | ImageKind::PremiumBanner)
    }
    pub fn to_string(&self) -> &str {
        return match self {
            ImageKind::Avatar => "avatar",
            ImageKind::Banner => "banner",
            ImageKind::PremiumAvatar => "premium_avatar",
            ImageKind::PremiumBanner => "premium_banner",
        };
    }
    pub fn from_string(str: &str) -> Option<ImageKind> {
        return match str {
            "avatar" => Some(ImageKind::Avatar),
            "banner" => Some(ImageKind::Banner),
            "premium_avatar" => Some(ImageKind::PremiumAvatar),
            "premium_banner" => Some(ImageKind::PremiumBanner),
            _ => None,
        };
    }
}

#[derive(FromRow)]
pub struct ImageQueueEntry {
    pub itemid: i32,
    pub url: String,
    pub kind: ImageKind,
}
