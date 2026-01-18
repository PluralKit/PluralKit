use std::iter::once;

use command_parser::token::TokensIterator;

use crate::utils::get_list_flags;

use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    edit()
}

pub fn system() -> (&'static str, [&'static str; 1]) {
    ("system", ["s"])
}

pub fn targeted() -> TokensIterator {
    tokens!(system(), SystemRef)
}

pub fn edit() -> impl Iterator<Item = Command> {
    let system = system();

    let system_new_cmd =
        once(
            command!(system, ("new", ["n"]), Optional(Remainder(("name", OpaqueString))) => "system_new")
                .help("Creates a new system")
        );

    let system_webhook = tokens!(system, ("webhook", ["hook"]));
    let system_webhook_cmd = [
        command!(system_webhook => "system_webhook_show").help("Shows your system's webhook URL"),
        command!(system_webhook, CLEAR => "system_webhook_clear")
            .flag(YES)
            .help("Clears your system's webhook URL"),
        command!(system_webhook, ("url", OpaqueString) => "system_webhook_set")
            .help("Sets your system's webhook URL"),
    ];

    let add_info_flags = |cmd: Command| {
        cmd.flag(("public", ["pub"]))
            .flag(("private", ["priv"]))
            .flag(ALL)
    };
    let system_info_cmd = [
        command!(system, Optional(SystemRef) => "system_info")
            .help("Shows information about your system"),
        command!(system, Optional(SystemRef), ("info", ["show", "view"]) => "system_info")
            .help("Shows information about your system"),
    ]
    .into_iter()
    .map(add_info_flags);

    let name = "name";
    let system_name_cmd = once(
        command!(system, Optional(SystemRef), name => "system_show_name")
            .help("Shows the systems name"),
    );
    let system_name_self = tokens!(system, name);
    let system_name_self_cmd = [
        command!(system_name_self, CLEAR => "system_clear_name")
            .flag(YES)
            .help("Clears your system's name"),
        command!(system_name_self, Remainder(("name", OpaqueString)) => "system_rename")
            .help("Renames your system"),
    ];

    let server_name = ("servername", ["sn", "guildname"]);
    let system_server_name_cmd = once(
        command!(system, Optional(SystemRef), server_name => "system_show_server_name")
            .help("Shows the system's server name"),
    );
    let system_server_name_self = tokens!(system, server_name);
    let system_server_name_self_cmd = [
        command!(system_server_name_self, CLEAR => "system_clear_server_name")
            .flag(YES)
            .help("Clears your system's server name"),
        command!(system_server_name_self, Remainder(("name", OpaqueString)) => "system_rename_server_name")
            .help("Renames your system's server name"),
    ];

    let description = ("description", ["desc", "d"]);
    let system_description_cmd = once(
        command!(system, Optional(SystemRef), description => "system_show_description")
            .help("Shows the system's description"),
    );
    let system_description_self = tokens!(system, description);
    let system_description_self_cmd = [
        command!(system_description_self, CLEAR => "system_clear_description")
        .flag(YES)
            .help("Clears your system's description"),
        command!(system_description_self, Remainder(("description", OpaqueString)) => "system_change_description")
            .help("Changes your system's description"),
    ];

    let color = ("color", ["colour"]);
    let system_color_cmd = once(
        command!(system, Optional(SystemRef), color => "system_show_color")
            .help("Shows the system's color"),
    );
    let system_color_self = tokens!(system, color);
    let system_color_self_cmd = [
        command!(system_color_self, CLEAR => "system_clear_color")
            .flag(YES)
            .help("Clears your system's color"),
        command!(system_color_self, ("color", OpaqueString) => "system_change_color")
            .help("Changes your system's color"),
    ];

    let tag = ("tag", ["suffix"]);
    let system_tag_cmd = once(
        command!(system, Optional(SystemRef), tag => "system_show_tag")
            .help("Shows the system's tag"),
    );
    let system_tag_self = tokens!(system, tag);
    let system_tag_self_cmd = [
        command!(system_tag_self, CLEAR => "system_clear_tag")
            .flag(YES)
            .help("Clears your system's tag"),
        command!(system_tag_self, Remainder(("tag", OpaqueString)) => "system_change_tag")
            .help("Changes your system's tag"),
    ];

    let servertag = ("servertag", ["st", "guildtag"]);
    let system_server_tag_cmd = once(
        command!(system, Optional(SystemRef) => "system_show_server_tag")
            .help("Shows the system's server tag"),
    );
    let system_server_tag_self = tokens!(system, servertag);
    let system_server_tag_self_cmd = [
        command!(system_server_tag_self, CLEAR => "system_clear_server_tag")
            .flag(YES)
            .help("Clears your system's server tag"),
        command!(system_server_tag_self, Remainder(("tag", OpaqueString)) => "system_change_server_tag")
            .help("Changes your system's server tag"),
    ];

    let pronouns = ("pronouns", ["prns"]);
    let system_pronouns_cmd = once(
        command!(system, Optional(SystemRef), pronouns => "system_show_pronouns")
            .help("Shows the system's pronouns"),
    );
    let system_pronouns_self = tokens!(system, pronouns);
    let system_pronouns_self_cmd = [
        command!(system_pronouns_self, CLEAR => "system_clear_pronouns")
            .flag(YES)
            .help("Clears your system's pronouns"),
        command!(system_pronouns_self, Remainder(("pronouns", OpaqueString)) => "system_change_pronouns")
            .help("Changes your system's pronouns"),
    ];

    let avatar = ("avatar", ["pfp"]);
    let system_avatar_cmd = once(
        command!(system, Optional(SystemRef), avatar => "system_show_avatar")
            .help("Shows the system's avatar"),
    );
    let system_avatar_self = tokens!(system, avatar);
    let system_avatar_self_cmd = [
        command!(system_avatar_self, CLEAR => "system_clear_avatar")
            .flag(YES)
            .help("Clears your system's avatar"),
        command!(system_avatar_self, ("avatar", Avatar) => "system_change_avatar")
            .help("Changes your system's avatar"),
    ];

    let serveravatar = ("serveravatar", ["spfp"]);
    let system_server_avatar_cmd = once(
        command!(system, Optional(SystemRef), serveravatar => "system_show_server_avatar")
            .help("Shows the system's server avatar"),
    );
    let system_server_avatar_self = tokens!(system, serveravatar);
    let system_server_avatar_self_cmd = [
        command!(system_server_avatar_self, CLEAR => "system_clear_server_avatar")
            .flag(YES)
            .help("Clears your system's server avatar"),
        command!(system_server_avatar_self, ("avatar", Avatar) => "system_change_server_avatar")
            .help("Changes your system's server avatar"),
    ];

    let banner = ("banner", ["cover"]);
    let system_banner_cmd = once(
        command!(system, Optional(SystemRef), banner => "system_show_banner")
            .help("Shows the system's banner"),
    );
    let system_banner_self = tokens!(system, banner);
    let system_banner_self_cmd = [
        command!(system_banner_self, CLEAR => "system_clear_banner")
            .flag(YES)
            .help("Clears your system's banner"),
        command!(system_banner_self, ("banner", Avatar) => "system_change_banner")
            .help("Changes your system's banner"),
    ];

    let system_proxy = tokens!(system, "proxy");
    let system_proxy_cmd = [
        command!(system_proxy => "system_show_proxy_current")
            .help("Shows your system's proxy setting for the guild you are in"),
        command!(system_proxy, Skip(Toggle) => "system_toggle_proxy_current")
            .help("Toggle your system's proxy for the guild you are in"),
        command!(system_proxy, GuildRef => "system_show_proxy")
            .help("Shows your system's proxy setting for a guild"),
        command!(system_proxy, GuildRef, Toggle => "system_toggle_proxy")
            .help("Toggle your system's proxy for a guild"),
    ];

    let system_privacy = tokens!(system, ("privacy", ["priv"]));
    let system_privacy_cmd = [
        command!(system_privacy => "system_show_privacy")
            .help("Shows your system's privacy settings"),
        command!(system_privacy, ALL, ("level", PrivacyLevel) => "system_change_privacy_all")
            .help("Changes all privacy settings for your system"),
        command!(system_privacy, ("privacy", SystemPrivacyTarget), ("level", PrivacyLevel) => "system_change_privacy")
            .help("Changes a specific privacy setting for your system"),
    ];

    let front = ("front", ["fronter", "fronters", "f"]);
    let make_front_history = |subcmd: TokensIterator| {
        command!(system, Optional(SystemRef), subcmd => "system_fronter_history")
            .help("Shows a system's front history")
            .flag(CLEAR)
    };
    let make_front_percent = |subcmd: TokensIterator| {
        command!(system, Optional(SystemRef), subcmd => "system_fronter_percent")
            .help("Shows a system's front breakdown")
            .flag(("duration", OpaqueString))
            .flag(("fronters-only", ["fo"]))
            .flag("flat")
    };
    let system_front_cmd = [
        command!(system, Optional(SystemRef), front => "system_fronter")
            .help("Shows a system's fronter(s)"),
        make_front_history(tokens!(front, ("history", ["h"]))),
        make_front_history(tokens!(("fronthistory", ["fh"]))),
        make_front_percent(tokens!(front, ("percent", ["p", "%"]))),
        make_front_percent(tokens!(("frontpercent", ["fp"]))),
    ];

    let search_param = Optional(Remainder(("query", OpaqueString)));
    let apply_list_opts = |cmd: Command| cmd.flags(get_list_flags());

    let members_subcmd = tokens!(("members", ["l", "ls", "list"]), search_param);
    let system_members_cmd = [
        command!(system, Optional(SystemRef), members_subcmd => "system_members")
            .help("Lists a system's members"),
        command!(members_subcmd => "system_members").help("Lists your system's members"),
    ]
    .map(apply_list_opts);

    let system_groups_cmd = once(
        command!(system, Optional(SystemRef), "groups", search_param => "system_groups")
            .help("Lists groups in a system"),
    )
    .map(apply_list_opts);

    let system_display_id_cmd = once(
        command!(system, Optional(SystemRef), "id" => "system_display_id")
            .help("Prints a system's ID"),
    );

    let system_delete = once(
        command!(system, ("delete", ["erase", "remove", "yeet"]) => "system_delete")
            .flag(("no-export", ["ne"]))
            .help("Deletes the system"),
    );

    let system_link = [
        command!("link", ("account", UserRef) => "system_link")
            .help("Links another Discord account to your system"),
        command!("unlink", ("account", OpaqueString) => "system_unlink")
            .help("Unlinks a Discord account from your system")
            .flag(YES),
    ];

    system_new_cmd
        .chain(system_webhook_cmd)
        .chain(system_name_self_cmd)
        .chain(system_server_name_self_cmd)
        .chain(system_description_self_cmd)
        .chain(system_color_self_cmd)
        .chain(system_tag_self_cmd)
        .chain(system_server_tag_self_cmd)
        .chain(system_pronouns_self_cmd)
        .chain(system_avatar_self_cmd)
        .chain(system_server_avatar_self_cmd)
        .chain(system_banner_self_cmd)
        .chain(system_delete)
        .chain(system_privacy_cmd)
        .chain(system_proxy_cmd)
        .chain(system_name_cmd)
        .chain(system_server_name_cmd)
        .chain(system_description_cmd)
        .chain(system_color_cmd)
        .chain(system_tag_cmd)
        .chain(system_server_tag_cmd)
        .chain(system_pronouns_cmd)
        .chain(system_avatar_cmd)
        .chain(system_server_avatar_cmd)
        .chain(system_banner_cmd)
        .chain(system_info_cmd)
        .chain(system_front_cmd)
        .chain(system_link)
        .chain(system_members_cmd)
        .chain(system_groups_cmd)
        .chain(system_display_id_cmd)
}
