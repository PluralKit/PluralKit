use twilight_http::Client;
use twilight_model::{
    channel::{embed::Embed, Message},
    id::{
        marker::{GuildMarker, UserMarker},
        Id,
    },
    util::ImageHash,
};
use twilight_util::builder::embed::{EmbedAuthorBuilder, EmbedBuilder, ImageSource};

async fn _fetch_additional_reply_info(_http: &Client) -> anyhow::Result<()> {
    Ok(())
}

pub fn create_reply_embed(
    guild_id: Id<GuildMarker>,
    replied_to: &Message,
) -> anyhow::Result<Embed> {
    // todo: guild avatars, guild nicknames
    // probably put this in fetch_additional_reply_info

    let author = {
        let icon = replied_to
            .author
            .avatar
            .map(|hash| get_avatar_url(replied_to.author.id, hash))
            .and_then(|url| ImageSource::url(url).ok());

        let mut builder = EmbedAuthorBuilder::new(replied_to.author.name.clone());
        if let Some(icon) = icon {
            builder = builder.icon_url(icon);
        };
        builder.build()
    };

    let content = {
        let jump_link = format!(
            "https://discord.com/channels/{}/{}/{}",
            guild_id, replied_to.channel_id, replied_to.id
        );

        let content = format!("**[Reply to:]({})** ", jump_link);
        // todo: properly add truncated content (including handling links/spoilers/etc)
        content
    };

    let builder = EmbedBuilder::new().description(content).author(author);
    Ok(builder.build())
}

fn get_avatar_url(user_id: Id<UserMarker>, hash: ImageHash) -> String {
    format!(
        "https://cdn.discordapp.com/avatars/{}/{}.png",
        user_id, hash
    )
}
