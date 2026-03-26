use std::str::FromStr;

use sqlx::{Pool, Postgres};
use uuid::Uuid;

use crate::{GroupId, GroupRef, MemberId, MemberRef, SwitchId, SwitchRef, SystemId, SystemRef};

pub trait ResolveId {
    type Out;
    #[allow(async_fn_in_trait)]
    async fn resolve_id(&self, db: &Pool<Postgres>) -> Result<Option<Self::Out>, sqlx::Error>;
    fn kind() -> &'static str;
}

impl TryFrom<&str> for SwitchRef {
    type Error = <Uuid as FromStr>::Err;
    fn try_from(s: &str) -> Result<Self, Self::Error> {
        Uuid::parse_str(s).map(SwitchRef::Uuid)
    }
}

impl From<&str> for GroupRef {
    fn from(s: &str) -> Self {
        let uuid_opt = Uuid::parse_str(s).map(GroupRef::Uuid).ok();
        let hid = GroupRef::Hid(parse_hid(s));
        uuid_opt.unwrap_or(hid)
    }
}

impl From<&str> for MemberRef {
    fn from(s: &str) -> Self {
        let uuid_opt = Uuid::parse_str(s).map(MemberRef::Uuid).ok();
        let hid = MemberRef::Hid(parse_hid(s));
        uuid_opt.unwrap_or(hid)
    }
}

impl From<&str> for SystemRef {
    fn from(s: &str) -> Self {
        let uuid_opt = Uuid::parse_str(s).map(SystemRef::Uuid).ok();
        let discord_acc_opt = s.parse::<i64>().map(SystemRef::DiscordAccountUid).ok();
        let hid = SystemRef::Hid(parse_hid(s));
        uuid_opt.or(discord_acc_opt).unwrap_or(hid)
    }
}

impl ResolveId for SystemRef {
    type Out = (SystemId,); // we need to wrap i32 into a tuple, because for some reason Postgres cannot parse a single i32 from a single column row,
    // but it can do that from a single-element tuple (i32,)
    async fn resolve_id(&self, db: &Pool<Postgres>) -> Result<Option<Self::Out>, sqlx::Error> {
        match self {
            SystemRef::Uuid(uuid) => {
                sqlx::query_as("select id from systems where uuid = $1")
                    .bind(uuid)
                    .fetch_optional(db)
                    .await
            }
            SystemRef::DiscordAccountUid(uid) => {
                sqlx::query_as("select system from accounts where uid = $1")
                    .bind(uid)
                    .fetch_optional(db)
                    .await
            }
            SystemRef::Hid(hid) => {
                sqlx::query_as("select id from systems where hid = $1")
                    .bind(hid)
                    .fetch_optional(db)
                    .await
            }
        }
    }

    fn kind() -> &'static str {
        "system"
    }
}

impl ResolveId for MemberRef {
    type Out = (MemberId, SystemId);
    async fn resolve_id(&self, db: &Pool<Postgres>) -> Result<Option<Self::Out>, sqlx::Error> {
        match self {
            MemberRef::Uuid(uuid) => {
                sqlx::query_as("select id, system from members where uuid = $1")
                    .bind(uuid)
                    .fetch_optional(db)
                    .await
            }
            MemberRef::Hid(hid) => {
                sqlx::query_as("select id, system from members where hid = $1")
                    .bind(hid)
                    .fetch_optional(db)
                    .await
            }
        }
    }

    fn kind() -> &'static str {
        "member"
    }
}

impl ResolveId for GroupRef {
    type Out = (GroupId, SystemId);
    async fn resolve_id(&self, db: &Pool<Postgres>) -> Result<Option<Self::Out>, sqlx::Error> {
        match self {
            GroupRef::Uuid(uuid) => {
                sqlx::query_as("select id, system from groups where uuid = $1")
                    .bind(uuid)
                    .fetch_optional(db)
                    .await
            }
            GroupRef::Hid(hid) => {
                sqlx::query_as("select id, system from groups where hid = $1")
                    .bind(hid)
                    .fetch_optional(db)
                    .await
            }
        }
    }

    fn kind() -> &'static str {
        "group"
    }
}

impl ResolveId for SwitchRef {
    type Out = (SwitchId, SystemId);
    async fn resolve_id(&self, db: &Pool<Postgres>) -> Result<Option<Self::Out>, sqlx::Error> {
        match self {
            SwitchRef::Uuid(uuid) => {
                sqlx::query_as("select id, system from switches where uuid = $1")
                    .bind(uuid)
                    .fetch_optional(db)
                    .await
            }
        }
    }

    fn kind() -> &'static str {
        "switch"
    }
}

pub fn parse_hid(hid: &str) -> String {
    if hid.len() > 7 || hid.len() < 5 {
        hid.to_string()
    } else {
        hid.to_lowercase().replace("-", "")
    }
}
