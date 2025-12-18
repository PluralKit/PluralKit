use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    [
        command!("invite" => "invite").help("Gets a link to invite PluralKit to other servers"),
        command!(("stats", ["status"]) => "stats")
            .help("Shows statistics and information about PluralKit"),
    ]
    .into_iter()
}
