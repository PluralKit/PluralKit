use sqlx::FromRow;

#[derive(FromRow, Debug, Clone)]
pub struct PKSystem {
    pub id: i32,
    pub name: Option<String>,
    pub tag: Option<String>,
    pub avatar_url: Option<String>,
}

#[derive(FromRow, Debug, Clone)]
pub struct PKMember {
    pub id: i32,
    pub system: i32,
    pub name: String,

    pub color: Option<String>,
    pub avatar_url: Option<String>,
    pub display_name: Option<String>,
    pub pronouns: Option<String>,
    pub description: Option<String>,
}

#[derive(FromRow, Debug, Clone)]
pub struct PKMessage {
    pub mid: i64,
    pub guild: Option<i64>,
    pub channel: i64,
    pub member_id: i32,
    pub sender: i64,
    pub original_mid: Option<i64>,
}

#[derive(FromRow, Debug, Clone)]
pub struct PKSystemGuild {
    pub system: i32,
    pub guild: i64,
    pub proxy_enabled: bool,
    pub tag: Option<String>,
    pub tag_enabled: bool,
}

#[derive(FromRow, Debug, Clone)]
pub struct PKMemberGuild {
    pub member: i32,
    pub guild: i64,
    pub display_name: Option<String>,
    pub avatar_url: Option<String>,
}
