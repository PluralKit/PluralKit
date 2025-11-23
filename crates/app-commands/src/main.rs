use twilight_model::{
    application::command::{Command, CommandType},
    guild::IntegrationApplication,
};
use twilight_util::builder::command::CommandBuilder;

#[libpk::main]
async fn main() -> anyhow::Result<()> {
    let discord = twilight_http::Client::builder()
        .token(
            libpk::config
                .discord
                .as_ref()
                .expect("missing discord config")
                .bot_token
                .clone(),
        )
        .build();

    let interaction = discord.interaction(twilight_model::id::Id::new(
        libpk::config
            .discord
            .as_ref()
            .expect("missing discord config")
            .client_id
            .clone()
            .get(),
    ));

    let commands = vec![
        // message commands
        // description must be empty string
        CommandBuilder::new("\u{2753} Message info", "", CommandType::Message).build(),
        CommandBuilder::new("\u{274c} Delete message", "", CommandType::Message).build(),
        CommandBuilder::new("\u{1f514} Ping author", "", CommandType::Message).build(),
    ];

    interaction.set_global_commands(&commands).await?;

    Ok(())
}
