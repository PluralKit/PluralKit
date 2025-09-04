use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let member = ("member", ["m"]);
    let member_target = tokens!(member, MemberRef);

    let name = ("name", ["n"]);
    let description = ("description", ["desc"]);
    let pronouns = ("pronouns", ["pronoun", "prns", "pn"]);
    let privacy = ("privacy", ["priv"]);
    let new = ("new", ["n"]);
    let banner = ("banner", ["bn"]);
    let color = ("color", ["colour"]);
    let birthday = ("birthday", ["bday", "bd"]);
    let display_name = ("displayname", ["dname", "dn"]);
    let server_name = ("servername", ["sname", "sn"]);
    let keep_proxy = ("keepproxy", ["kp"]);
    let server_keep_proxy = ("serverkeepproxy", ["skp"]);
    let autoproxy = ("autoproxy", ["ap"]);
    let proxy = ("proxy", ["tags", "proxytags", "brackets"]);
    let tts = ("tts", ["texttospeech"]);
    let delete = ("delete", ["del", "remove"]);

    let member_new_cmd = [
        command!(member, new, ("name", OpaqueString) => "member_new")
            .help("Creates a new system member"),
    ]
    .into_iter();

    let member_info_cmd = [command!(member_target => "member_show")
        .flag("pt")
        .help("Shows information about a member")]
    .into_iter();

    let member_name_cmd = {
        let member_name = tokens!(member_target, name);
        [
            command!(member_name => "member_name_show").help("Shows a member's name"),
            command!(member_name, ("name", OpaqueStringRemainder) => "member_name_update")
                .help("Changes a member's name"),
        ]
        .into_iter()
    };

    let member_description_cmd = {
        let member_desc = tokens!(member_target, description);
        [
            command!(member_desc => "member_desc_show").help("Shows a member's description"),
            command!(member_desc, ("clear", ["c"]) => "member_desc_clear")
                .flag(("yes", ["y"]))
                .help("Clears a member's description"),
            command!(member_desc, ("description", OpaqueStringRemainder) => "member_desc_update")
                .help("Changes a member's description"),
        ]
        .into_iter()
    };

    let member_privacy_cmd = {
        let member_privacy = tokens!(member_target, privacy);
        [
            command!(member_privacy => "member_privacy_show")
                .help("Displays a member's current privacy settings"),
            command!(
                member_privacy, MemberPrivacyTarget, ("new_privacy_level", PrivacyLevel)
                => "member_privacy_update"
            )
            .help("Changes a member's privacy settings"),
        ]
        .into_iter()
    };

    let member_pronouns_cmd = {
        let member_pronouns = tokens!(member_target, pronouns);
        [
            command!(member_pronouns => "member_pronouns_show")
                .help("Shows a member's pronouns"),
            command!(member_pronouns, ("pronouns", OpaqueStringRemainder) => "member_pronouns_update")
                .help("Changes a member's pronouns"),
            command!(member_pronouns, ("clear", ["c"]) => "member_pronouns_clear")
                .flag(("yes", ["y"]))
                .help("Clears a member's pronouns"),
        ].into_iter()
    };

    let member_banner_cmd = {
        let member_banner = tokens!(member_target, banner);
        [
            command!(member_banner => "member_banner_show").help("Shows a member's banner image"),
            command!(member_banner, ("banner", Avatar) => "member_banner_update")
                .help("Changes a member's banner image"),
            command!(member_banner, ("clear", ["c"]) => "member_banner_clear")
                .flag(("yes", ["y"]))
                .help("Clears a member's banner image"),
        ]
        .into_iter()
    };

    let member_color_cmd = {
        let member_color = tokens!(member_target, color);
        [
            command!(member_color => "member_color_show").help("Shows a member's color"),
            command!(member_color, ("color", OpaqueString) => "member_color_update")
                .help("Changes a member's color"),
            command!(member_color, ("clear", ["c"]) => "member_color_clear")
                .flag(("yes", ["y"]))
                .help("Clears a member's color"),
        ]
        .into_iter()
    };

    let member_birthday_cmd = {
        let member_birthday = tokens!(member_target, birthday);
        [
            command!(member_birthday => "member_birthday_show").help("Shows a member's birthday"),
            command!(member_birthday, ("birthday", OpaqueString) => "member_birthday_update")
                .help("Changes a member's birthday"),
            command!(member_birthday, ("clear", ["c"]) => "member_birthday_clear")
                .flag(("yes", ["y"]))
                .help("Clears a member's birthday"),
        ]
        .into_iter()
    };

    let member_display_name_cmd = {
        let member_display_name = tokens!(member_target, display_name);
        [
            command!(member_display_name => "member_displayname_show")
                .help("Shows a member's display name"),
            command!(member_display_name, ("name", OpaqueStringRemainder) => "member_displayname_update")
                .help("Changes a member's display name"),
            command!(member_display_name, ("clear", ["c"]) => "member_displayname_clear")
                .flag(("yes", ["y"]))
                .help("Clears a member's display name"),
        ].into_iter()
    };

    let member_server_name_cmd = {
        let member_server_name = tokens!(member_target, server_name);
        [
            command!(member_server_name => "member_servername_show")
                .help("Shows a member's server name"),
            command!(member_server_name, ("name", OpaqueStringRemainder) => "member_servername_update")
                .help("Changes a member's server name"),
            command!(member_server_name, ("clear", ["c"]) => "member_servername_clear")
                .flag(("yes", ["y"]))
                .help("Clears a member's server name"),
        ].into_iter()
    };

    let member_proxy_cmd = {
        let member_proxy = tokens!(member_target, proxy);
        [
            command!(member_proxy => "member_proxy_show")
                .help("Shows a member's proxy tags"),
            command!(member_proxy, ("tags", OpaqueString) => "member_proxy_set")
                .help("Sets a member's proxy tags"),
            command!(member_proxy, ("add", ["a"]), ("tag", OpaqueString) => "member_proxy_add")
                .help("Adds proxy tag to a member"),
            command!(member_proxy, ("remove", ["r", "rm"]), ("tag", OpaqueString) => "member_proxy_remove")
                .help("Removes proxy tag from a member"),
            command!(member_proxy, ("clear", ["c"]) => "member_proxy_clear")
                .flag(("yes", ["y"]))
                .help("Clears all proxy tags from a member"),
        ].into_iter()
    };

    let member_proxy_settings_cmd = {
        let member_keep_proxy = tokens!(member_target, keep_proxy);
        let member_server_keep_proxy = tokens!(member_target, server_keep_proxy);
        [
            command!(member_keep_proxy => "member_keepproxy_show")
                .help("Shows a member's keep-proxy setting"),
            command!(member_keep_proxy, ("value", Toggle) => "member_keepproxy_update")
                .help("Changes a member's keep-proxy setting"),
            command!(member_server_keep_proxy => "member_server_keepproxy_show")
                .help("Shows a member's server-specific keep-proxy setting"),
            command!(member_server_keep_proxy, ("value", Toggle) => "member_server_keepproxy_update")
                .help("Changes a member's server-specific keep-proxy setting"),
            command!(member_server_keep_proxy, ("clear", ["c"]) => "member_server_keepproxy_clear")
                .flag(("yes", ["y"]))
                .help("Clears a member's server-specific keep-proxy setting"),
        ].into_iter()
    };

    let member_message_settings_cmd = {
        let member_tts = tokens!(member_target, tts);
        let member_autoproxy = tokens!(member_target, autoproxy);
        [
            command!(member_tts => "member_tts_show")
                .help("Shows whether a member's messages are sent as TTS"),
            command!(member_tts, ("value", Toggle) => "member_tts_update")
                .help("Changes whether a member's messages are sent as TTS"),
            command!(member_autoproxy => "member_autoproxy_show")
                .help("Shows whether a member can be autoproxied"),
            command!(member_autoproxy, ("value", Toggle) => "member_autoproxy_update")
                .help("Changes whether a member can be autoproxied"),
        ]
        .into_iter()
    };

    let member_avatar_cmd = {
        let member_avatar = tokens!(
            member_target,
            (
                "avatar",
                ["profile", "picture", "icon", "image", "pfp", "pic"]
            )
        );
        [
            command!(member_avatar => "member_avatar_show").help("Shows a member's avatar"),
            command!(member_avatar, ("avatar", Avatar) => "member_avatar_update")
                .help("Changes a member's avatar"),
            command!(member_avatar, ("clear", ["c"]) => "member_avatar_clear")
                .flag(("yes", ["y"]))
                .help("Clears a member's avatar"),
        ]
        .into_iter()
    };

    let member_webhook_avatar_cmd = {
        let member_webhook_avatar = tokens!(
            member_target,
            (
                "proxyavatar",
                [
                    "proxypfp",
                    "webhookavatar",
                    "webhookpfp",
                    "pa",
                    "pavatar",
                    "ppfp"
                ]
            )
        );
        [
            command!(member_webhook_avatar => "member_webhook_avatar_show")
                .help("Shows a member's proxy avatar"),
            command!(member_webhook_avatar, ("avatar", Avatar) => "member_webhook_avatar_update")
                .help("Changes a member's proxy avatar"),
            command!(member_webhook_avatar, ("clear", ["c"]) => "member_webhook_avatar_clear")
                .flag(("yes", ["y"]))
                .help("Clears a member's proxy avatar"),
        ]
        .into_iter()
    };

    let member_server_avatar_cmd = {
        let member_server_avatar = tokens!(
            member_target,
            (
                "serveravatar",
                [
                    "sa",
                    "servericon",
                    "serverimage",
                    "serverpfp",
                    "serverpic",
                    "savatar",
                    "spic",
                    "guildavatar",
                    "guildpic",
                    "guildicon",
                    "sicon",
                    "spfp"
                ]
            )
        );
        [
            command!(member_server_avatar => "member_server_avatar_show")
                .help("Shows a member's server-specific avatar"),
            command!(member_server_avatar, ("avatar", Avatar) => "member_server_avatar_update")
                .help("Changes a member's server-specific avatar"),
            command!(member_server_avatar, ("clear", ["c"]) => "member_server_avatar_clear")
                .flag(("yes", ["y"]))
                .help("Clears a member's server-specific avatar"),
        ]
        .into_iter()
    };

    let member_avatar_cmds = member_avatar_cmd
        .chain(member_webhook_avatar_cmd)
        .chain(member_server_avatar_cmd);

    let member_delete_cmd =
        [command!(member_target, delete => "member_delete").help("Deletes a member")].into_iter();

    let member_easter_eggs =
        [command!(member_target, "soulscream" => "member_soulscream").show_in_suggestions(false)]
            .into_iter();

    member_new_cmd
        .chain(member_info_cmd)
        .chain(member_name_cmd)
        .chain(member_description_cmd)
        .chain(member_privacy_cmd)
        .chain(member_pronouns_cmd)
        .chain(member_banner_cmd)
        .chain(member_color_cmd)
        .chain(member_birthday_cmd)
        .chain(member_display_name_cmd)
        .chain(member_server_name_cmd)
        .chain(member_proxy_cmd)
        .chain(member_avatar_cmds)
        .chain(member_proxy_settings_cmd)
        .chain(member_message_settings_cmd)
        .chain(member_delete_cmd)
        .chain(member_easter_eggs)
}
