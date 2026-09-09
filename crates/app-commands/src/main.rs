use twilight_model::application::command::CommandType;
use twilight_util::builder::command::{ CommandBuilder, SubCommandBuilder, StringBuilder };

#[libpk::main]
async fn main() -> anyhow::Result<()> {
    let discord = twilight_http::Client
        ::builder()
        .token(libpk::config.discord().bot_token.clone())
        .build();

    let interaction = discord.interaction(
        twilight_model::id::Id::new(libpk::config.discord().client_id.clone().get())
    );

    let commands = vec![
        // message commands
        // description must be empty string
        CommandBuilder::new("\u{2753} Message info", "", CommandType::Message).build(),
        CommandBuilder::new("\u{274c} Delete message", "", CommandType::Message).build(),
        CommandBuilder::new("\u{1f514} Ping author", "", CommandType::Message).build(),

        // slash commands
        CommandBuilder::new("system", "Commands run on a PK system", CommandType::ChatInput)
            .option(
                SubCommandBuilder::new(
                    "new",
                    "Makes a new PK system if one is not already on your account"
                )
                    .option(StringBuilder::new("name", "The name of the new system"))
                    .build()
            )
            .build()
    ];

    interaction.set_global_commands(&commands).await?;

    Ok(())
}
