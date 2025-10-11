use super::*;

pub fn admin() -> &'static str {
    "admin"
}

pub fn cmds() -> impl Iterator<Item = Command> {
    let admin = admin();

    let abuselog = tokens!(admin, ("abuselog", ["al"]));
    let make_abuselog_cmds = |log_param: Parameter| {
        [
            command!(abuselog, ("show", ["s"]), log_param => format!("admin_abuselog_show_{}", log_param.name()))
                .help("Shows an abuse log entry"),
            command!(abuselog, ("flagdeny", ["fd"]), log_param, Optional(("value", Toggle)) => format!("admin_abuselog_flag_deny_{}", log_param.name()))
                .help("Sets the deny flag on an abuse log entry"),
            command!(abuselog, ("description", ["desc"]), log_param, Optional(("desc", OpaqueStringRemainder)) => format!("admin_abuselog_description_{}", log_param.name()))
                .flag(CLEAR)
                .flag(YES)
                .help("Sets the description of an abuse log entry"),
            command!(abuselog, ("adduser", ["au"]), log_param => format!("admin_abuselog_add_user_{}", log_param.name()))
                .help("Adds a user to an abuse log entry"),
            command!(abuselog, ("removeuser", ["ru"]), log_param => format!("admin_abuselog_remove_user_{}", log_param.name()))
                .help("Removes a user from an abuse log entry"),
            command!(abuselog, ("delete", ["d"]), log_param => format!("admin_abuselog_delete_{}", log_param.name()))
                .help("Deletes an abuse log entry"),
        ].into_iter()
    };
    let abuselog_cmds = [
        command!(abuselog, ("create", ["c", "new"]), ("account", UserRef), Optional(("description", OpaqueStringRemainder)) => "admin_abuselog_create")
            .flag(("deny-boy-usage", ["deny"]))
            .help("Creates an abuse log entry")
    ]
    .into_iter()
    .chain(make_abuselog_cmds(Skip(("account", UserRef)).into())) // falls through to log_id
    .chain(make_abuselog_cmds(("log_id", OpaqueString).into()));

    [
        command!(admin, ("updatesystemid", ["usid"]), SystemRef, ("new_hid", OpaqueString) => "admin_update_system_id")
            .flag(YES)
            .help("Updates a system's ID"),
        command!(admin, ("updatememberid", ["umid"]), MemberRef, ("new_hid", OpaqueString) => "admin_update_member_id")
            .flag(YES)
            .help("Updates a member's ID"),
        command!(admin, ("updategroupid", ["ugid"]), GroupRef, ("new_hid", OpaqueString) => "admin_update_group_id")
            .flag(YES)
            .help("Updates a group's ID"),
        command!(admin, ("rerollsystemid", ["rsid"]), SystemRef => "admin_reroll_system_id")
            .flag(YES)
            .help("Rerolls a system's ID"),
        command!(admin, ("rerollmemberid", ["rmid"]), MemberRef => "admin_reroll_member_id")
            .flag(YES)
            .help("Rerolls a member's ID"),
        command!(admin, ("rerollgroupid", ["rgid"]), GroupRef => "admin_reroll_group_id")
            .flag(YES)
            .help("Rerolls a group's ID"),
        command!(admin, ("updatememberlimit", ["uml"]), SystemRef, Optional(("limit", OpaqueInt)) => "admin_system_member_limit")
            .flag(YES)
            .help("Updates a system's member limit"),
        command!(admin, ("updategrouplimit", ["ugl"]), SystemRef, Optional(("limit", OpaqueInt)) => "admin_system_group_limit")
            .flag(YES)
            .help("Updates a system's group limit"),
        command!(admin, ("systemrecover", ["sr"]), ("token", OpaqueString), ("account", UserRef) => "admin_system_recover")
            .flag(YES)
            .flag(("reroll-token", ["rt"]))
            .help("Recovers a system"),
        command!(admin, ("systemdelete", ["sd"]), SystemRef => "admin_system_delete")
            .help("Deletes a system"),
        command!(admin, ("sendmessage", ["sendmsg"]), ("account", UserRef), ("content", OpaqueStringRemainder) => "admin_send_message")
            .help("Sends a message to a user"),
    ]
    .into_iter()
    .chain(abuselog_cmds)
    .map(|cmd| cmd.show_in_suggestions(false))
}
