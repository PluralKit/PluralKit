use serde::{Deserialize, Serialize};
use sqlx::{
    types::chrono::{DateTime, Utc},
    FromRow,
};
use uuid::Uuid;

#[derive(FromRow)]
pub struct ImageMeta {
    pub id: String,
    pub kind: ImageKind,
    pub content_type: String,
    pub url: String,
    pub file_size: i32,
    pub width: i32,
    pub height: i32,
    pub uploaded_at: Option<DateTime<Utc>>,

    pub original_url: Option<String>,
    pub original_attachment_id: Option<i64>,
    pub original_file_size: Option<i32>,
    pub original_type: Option<String>,
    pub uploaded_by_account: Option<i64>,
    pub uploaded_by_system: Option<Uuid>,
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
}

impl ImageKind {
    pub fn size(&self) -> (u32, u32) {
        match self {
            Self::Avatar => (512, 512),
            Self::Banner => (1024, 1024),
        }
    }
}

#[derive(FromRow)]
pub struct ImageQueueEntry {
    pub itemid: i32,
    pub url: String,
    pub kind: ImageKind,
}
