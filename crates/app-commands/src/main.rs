use twilight_model::application::command::CommandType;
use twilight_util::builder::command::{
    CommandBuilder, StringBuilder, SubCommandBuilder, SubCommandGroupBuilder, UserBuilder,
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
    let id_option = StringBuilder::new(
        "id",
        "ID of system or discord account to fetch system of. Only use this *or* account.",
    );
    let account_option = UserBuilder::new(
        "account",
        "Discord account to fetch the system of. Only use this *or* id.",
    );

    // Show/set/clear is a very common set of subcommands
    macro_rules! set_show_clear_option {
        ($field:literal, $max_length:literal) => {
            SubCommandGroupBuilder::new($field, "View a system's tag or change your own")
                .subcommands([
                    SubCommandBuilder::new("show", format!("Show a system's {}", $field))
                        .option(id_option.clone())
                        .option(account_option.clone()),
                    SubCommandBuilder::new("set", format!("Set your system {}", $field)).option(
                        StringBuilder::new($field, format!("New {}", $field))
                            .required(true)
                            .min_length(1)
                            .max_length($max_length),
                    ),
                    SubCommandBuilder::new("clear", format!("Clear your system's {}", $field)),
                ])
            .build()
        };
    }

    // TODO: Figure out how to make the max_lengths here and Limits in PluralKit.Core use the same source of truth
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
            .option(set_show_clear_option!("tag", 79))
            .option(set_show_clear_option!("description", 1000))
        .build(),
    ];

    interaction.set_global_commands(&commands).await?;

    Ok(())
}
