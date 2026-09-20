use twilight_model::application::command::{CommandOption, CommandType};
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

    // These are the standard options used in multiple subcommands
    // Abstracted up here so that the summary text can be easily changed
    let id_option = || {
        StringBuilder::new(
            "id",
            "ID of system or discord account to fetch system of. Only use this *or* account.",
        )
    };
    let account_option = || {
        UserBuilder::new(
            "account",
            "Discord account to fetch the system of. Only use this *or* id.",
        )
    };
    let guild_option = || {
        StringBuilder::new(
            "server-id",
            "ID of a server to use instead of the current one (required if running command in a DM)",
        )
    };

    // Show/set/clear is a very common set of subcommands
    let system_standard_options =
        |field: &str, max_length: u16, guild_target: bool| -> CommandOption {
            let show = SubCommandBuilder::new("show", format!("Show a system's {}", field))
                .option(id_option())
                .option(account_option());
            let set = SubCommandBuilder::new("set", format!("Set your system {}", field)).option(
                StringBuilder::new(field, format!("New {}", field))
                    .required(true)
                    .min_length(1)
                    .max_length(max_length),
            );
            let clear = SubCommandBuilder::new("clear", format!("Clear your system's {}", field));

            let show = if guild_target {
                show.option(guild_option())
            } else {
                show
            };
            let set = if guild_target {
                set.option(guild_option())
            } else {
                set
            };
            let clear = if guild_target {
                clear.option(guild_option())
            } else {
                clear
            };

            return SubCommandGroupBuilder::new(
                field,
                format!("View a system's {} or change your own", field),
            )
            .subcommands([show, set, clear])
            .build();
        };

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
                .option(id_option())
                .option(account_option())
                .build()
            )
            .option(system_standard_options("name", 100, false))
            .option(system_standard_options("servername", 100, true))
            .option(system_standard_options("tag", 79, false))
            .option(system_standard_options("description", 1000, false))
        .build(),
    ];

    interaction.set_global_commands(&commands).await?;

    Ok(())
}
