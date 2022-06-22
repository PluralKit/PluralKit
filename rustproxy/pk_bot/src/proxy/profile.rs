use futures::try_join;
use sqlx::PgPool;

use crate::{
    db,
    model::{PKMember, PKMemberGuild, PKSystem, PKSystemGuild},
};

// inventing the term "proxy profile" here to describe the info needed to work out the webhook name+avatar
// arbitrary choice to put the source models in the struct and logic in methods, could just as well have had a function to do the math and put the results in a struct
pub struct ProxyProfile {
    system: PKSystem,
    member: PKMember,
    system_guild: Option<PKSystemGuild>,
    member_guild: Option<PKMemberGuild>,
}

impl ProxyProfile {
    pub fn name(&self) -> &str {
        let member_name = &self.member.name;
        let display_name = self.member.display_name.as_deref();
        let server_name = self
            .member_guild
            .as_ref()
            .and_then(|x| x.display_name.as_deref());
        server_name.or(display_name).unwrap_or(member_name)
    }

    pub fn avatar_url(&self) -> Option<&str> {
        let system_avatar = self.system.avatar_url.as_deref();
        let member_avatar = self.member.avatar_url.as_deref();
        let server_avatar = self
            .member_guild
            .as_ref()
            .and_then(|x| x.avatar_url.as_deref());
        server_avatar.or(member_avatar).or(system_avatar)
    }

    pub fn tag(&self) -> Option<&str> {
        let server_tag = self.system_guild.as_ref().and_then(|x| x.tag.as_deref());
        let system_tag = self.system.tag.as_deref();
        server_tag.or(system_tag)
    }

    pub fn formatted_name(&self) -> String {
        let mut name = if let Some(tag) = self.tag() {
            format!("{} {}", self.name(), tag)
        } else {
            self.name().to_string()
        };

        if name.len() == 1 {
            name.push('\u{17b5}');
        }

        name
    }
}

pub async fn fetch_proxy_profile(
    pool: &PgPool,
    guild_id: u64,
    system_id: i32,
    member_id: i32,
) -> anyhow::Result<ProxyProfile> {
    // todo: this should be a db view with joins
    // this is all the info that proxy_members returned, so a single-member version of that could work nicely
    let system = db::get_system_by_id(pool, system_id);
    let member = db::get_member_by_id(pool, member_id);
    let system_guild = db::get_system_guild(pool, system_id, guild_id as i64);
    let member_guild = db::get_member_guild(pool, member_id, guild_id as i64);

    let (system, member, system_guild, member_guild) =
        try_join!(system, member, system_guild, member_guild)?;

    let system = system.ok_or_else(|| anyhow::anyhow!("could not find system"))?;
    let member = member.ok_or_else(|| anyhow::anyhow!("could not find member"))?;

    Ok(ProxyProfile {
        system,
        member,
        system_guild,
        member_guild,
    })
}

#[cfg(test)]
mod tests {
    // todo: this code is gonna be easy to unit test so we should do that
}