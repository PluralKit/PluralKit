use twilight_model::application::command::CommandType;
use twilight_util::builder::command::{
    BooleanBuilder, CommandBuilder, StringBuilder, SubCommandBuilder, SubCommandGroupBuilder,
    UserBuilder,
};

#[libpk::main]
async fn main() -> anyhow::Result<()> {
    let discord = twilight_http::Client::builder()
        .token(libpk::config.discord().bot_token.clone())
        .build();

    let interaction = discord.interaction(twilight_model::id::Id::new(
        libpk::config.discord().client_id.clone().get(),
    ));

    // These are the standard options for any subcommand that can target a different system
    // Abstracted up here so that the summary text can be easily changed
    let id_option = StringBuilder::new("id", "ID of system or discord account to fetch system of. Only use this *or* account.");
    let account_option = UserBuilder::new("account", "Discord account to fetch the system of. Only use this *or* id.");

    let commands = vec![
        // message commands
        // description must be empty string
        CommandBuilder::new("\u{2753} Message info", "", CommandType::Message).build(),
        CommandBuilder::new("\u{274c} Delete message", "", CommandType::Message).build(),
        CommandBuilder::new("\u{1f514} Ping author", "", CommandType::Message).build(),
        // slash commands
        CommandBuilder::new(
            "system",
            "Commands run on a PK system",
            CommandType::ChatInput
        )
        .option(
            SubCommandBuilder::new(
                "new",
                "Makes a new PK system if one is not already on your account",
            )
            .option(StringBuilder::new("name", "The name of the new system"))
            .build()
        )
        .option(
            SubCommandBuilder::new(
                "info",
                "Show information about a PK system, defaults to the one on the current account if no target given"
            )
            .option(id_option.clone())
            .option(account_option.clone())
            .build()
        )
        .option(SubCommandGroupBuilder::new("tag", "Set, clear, or view a system's tag")
            .subcommands([
                SubCommandBuilder::new("show", "View a system's tag")
                    .option(id_option.clone())
                    .option(account_option.clone()),
                    // TODO: Figure out how to make this and MaxSystemTagLength in PluralKit.Core use the same source of truth
                SubCommandBuilder::new("set", "Set your system tag")
                    .option(StringBuilder::new("tag", "New tag").required(true).min_length(1).max_length(79)),
                SubCommandBuilder::new("clear", "Clear your system's tag"),
            ])
            .build()
        )
        .build(),
    ];

    interaction.set_global_commands(&commands).await?;

    Ok(())
}
