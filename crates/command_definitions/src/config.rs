use command_parser::parameter;

use super::*;

pub fn cmds() -> impl IntoIterator<Item = Command> {
    let cfg = ("config", ["cfg", "configure"]);

    let base = [command!(cfg => "cfg_show").help("Shows the current configuration")];

    let ap = tokens!(cfg, ("autoproxy", ["ap"]));
    let ap_account = tokens!(ap, ("account", ["ac"]));
    let ap_timeout = tokens!(ap, ("timeout", ["tm"]));
    let autoproxy = [
        command!(ap_account => "cfg_ap_account_show")
            .help("Shows autoproxy status for the account"),
        command!(ap_account, Toggle => "cfg_ap_account_update")
            .help("Toggles autoproxy globally for the current account"),
        command!(ap_timeout => "cfg_ap_timeout_show").help("Shows the autoproxy timeout"),
        command!(ap_timeout, RESET => "cfg_ap_timeout_reset").help("Resets the autoproxy timeout"),
        command!(ap_timeout, parameter::Toggle::Off => "cfg_ap_timeout_off")
            .help("Disables the autoproxy timeout"),
        command!(ap_timeout, ("timeout", OpaqueString) => "cfg_ap_timeout_update")
            .help("Sets the latch timeout duration for your system"),
    ];

    let timezone_tokens = tokens!(cfg, ("timezone", ["zone", "tz"]));
    let timezone = [
        command!(timezone_tokens => "cfg_timezone_show").help("Shows the system timezone"),
        command!(timezone_tokens, RESET => "cfg_timezone_reset").help("Resets the system timezone"),
        command!(timezone_tokens, ("timezone", OpaqueString) => "cfg_timezone_update")
            .help("Changes your system's time zone"),
    ];

    let ping_tokens = tokens!(cfg, "ping");
    let ping = [
        command!(ping_tokens => "cfg_ping_show").help("Shows ping preferences"),
        command!(ping_tokens, Toggle => "cfg_ping_update")
            .help("Changes your system's ping preferences"),
    ];

    let priv_ = ("private", ["priv"]);
    let member_privacy = tokens!(cfg, priv_, ("member", ["mem"]));
    let member_privacy_short = tokens!(cfg, "mp");
    let group_privacy = tokens!(cfg, priv_, ("group", ["grp"]));
    let group_privacy_short = tokens!(cfg, "gp");
    let privacy = [
        command!(member_privacy => "cfg_member_privacy_show")
            .help("Shows the default privacy for new members"),
        command!(member_privacy, Toggle => "cfg_member_privacy_update")
            .help("Sets whether member privacy is automatically set to private when creating a new member"),
        command!(member_privacy_short => "cfg_member_privacy_show")
            .help("Shows the default privacy for new members"),
        command!(member_privacy_short, Toggle => "cfg_member_privacy_update")
            .help("Sets whether member privacy is automatically set to private when creating a new member"),
        command!(group_privacy => "cfg_group_privacy_show")
            .help("Shows the default privacy for new groups"),
        command!(group_privacy, Toggle => "cfg_group_privacy_update")
            .help("Sets whether group privacy is automatically set to private when creating a new group"),
        command!(group_privacy_short => "cfg_group_privacy_show")
            .help("Shows the default privacy for new groups"),
        command!(group_privacy_short, Toggle => "cfg_group_privacy_update")
            .help("Sets whether group privacy is automatically set to private when creating a new group"),
    ];

    let show = "show";
    let show_private = tokens!(cfg, show, priv_);
    let show_private_short = tokens!(cfg, "sp");
    let private_info = [
        command!(show_private => "cfg_show_private_info_show")
            .help("Shows whether private info is shown"),
        command!(show_private, Toggle => "cfg_show_private_info_update")
            .help("Sets whether private information is shown to linked accounts by default"),
        command!(show_private_short => "cfg_show_private_info_show")
            .help("Shows whether private info is shown"),
        command!(show_private_short, Toggle => "cfg_show_private_info_update")
            .help("Sets whether private information is shown to linked accounts by default"),
    ];

    let proxy = ("proxy", ["px"]);
    let proxy_case = tokens!(cfg, proxy, ("case", ["caps", "capitalize", "capitalise"]));
    let proxy_error = tokens!(cfg, proxy, ("error", ["errors"]));
    let proxy_error_short = tokens!(cfg, "pe");
    let proxy_switch = tokens!(cfg, proxy, "switch");
    let proxy_switch_short = tokens!(cfg, ("proxyswitch", ["ps"]));
    let proxy_settings = [
        command!(proxy_case => "cfg_case_sensitive_proxy_tags_show")
            .help("Shows whether proxy tags are case-sensitive"),
        command!(proxy_case, Toggle => "cfg_case_sensitive_proxy_tags_update")
            .help("Toggles case sensitivity for proxy tags"),
        command!(proxy_error => "cfg_proxy_error_message_show")
            .help("Shows whether proxy error messages are enabled"),
        command!(proxy_error, Toggle => "cfg_proxy_error_message_update")
            .help("Toggles proxy error messages"),
        command!(proxy_error_short => "cfg_proxy_error_message_show")
            .help("Shows whether proxy error messages are enabled"),
        command!(proxy_error_short, Toggle => "cfg_proxy_error_message_update")
            .help("Toggles proxy error messages"),
        command!(proxy_switch => "cfg_proxy_switch_show").help("Shows the proxy switch behavior"),
        command!(proxy_switch, ProxySwitchAction => "cfg_proxy_switch_update")
            .help("Sets the switching behavior when proxy tags are used"),
        command!(proxy_switch_short => "cfg_proxy_switch_show")
            .help("Shows the proxy switch behavior"),
        command!(proxy_switch_short, ProxySwitchAction => "cfg_proxy_switch_update")
            .help("Sets the switching behavior when proxy tags are used"),
    ];

    let id = ("id", ["ids"]);
    let split_id = tokens!(cfg, "split", id);
    let split_id_short = tokens!(cfg, ("sid", ["sids"]));
    let cap_id = tokens!(cfg, ("cap", ["caps", "capitalize", "capitalise"]), id);
    let cap_id_short = tokens!(cfg, ("capid", ["capids"]));
    let pad = ("pad", ["padding"]);
    let pad_id = tokens!(cfg, pad, id);
    let id_pad = tokens!(cfg, id, pad);
    let id_pad_short = tokens!(cfg, ("idpad", ["padid", "padids"]));
    let id_settings = [
        command!(split_id => "cfg_hid_split_show").help("Shows whether IDs are split in lists"),
        command!(split_id, Toggle => "cfg_hid_split_update").help("Toggles splitting IDs in lists"),
        command!(split_id_short => "cfg_hid_split_show")
            .help("Shows whether IDs are split in lists"),
        command!(split_id_short, Toggle => "cfg_hid_split_update")
            .help("Toggles splitting IDs in lists"),
        command!(cap_id => "cfg_hid_caps_show").help("Shows whether IDs are capitalized in lists"),
        command!(cap_id, Toggle => "cfg_hid_caps_update")
            .help("Toggles capitalization of IDs in lists"),
        command!(cap_id_short => "cfg_hid_caps_show")
            .help("Shows whether IDs are capitalized in lists"),
        command!(cap_id_short, Toggle => "cfg_hid_caps_update")
            .help("Toggles capitalization of IDs in lists"),
        command!(pad_id => "cfg_hid_padding_show").help("Shows the ID padding for lists"),
        command!(pad_id, ("padding", OpaqueString) => "cfg_hid_padding_update")
            .help("Sets the ID padding for lists"),
        command!(id_pad => "cfg_hid_padding_show").help("Shows the ID padding for lists"),
        command!(id_pad, ("padding", OpaqueString) => "cfg_hid_padding_update")
            .help("Sets the ID padding for lists"),
        command!(id_pad_short => "cfg_hid_padding_show").help("Shows the ID padding for lists"),
        command!(id_pad_short, ("padding", OpaqueString) => "cfg_hid_padding_update")
            .help("Sets the ID padding for lists"),
    ];

    let show_color = tokens!(cfg, show, ("color", ["colour", "colors", "colours"]));
    let show_color_short = tokens!(
        cfg,
        (
            "showcolor",
            [
                "showcolour",
                "showcolors",
                "showcolours",
                "colorcode",
                "colorhex"
            ]
        )
    );
    let color_settings = [
        command!(show_color => "cfg_card_show_color_hex_show")
            .help("Shows whether color hex codes are shown on cards"),
        command!(show_color, Toggle => "cfg_card_show_color_hex_update")
            .help("Toggles showing color hex codes on cards"),
        command!(show_color_short => "cfg_card_show_color_hex_show")
            .help("Shows whether color hex codes are shown on cards"),
        command!(show_color_short, Toggle => "cfg_card_show_color_hex_update")
            .help("Toggles showing color hex codes on cards"),
    ];

    let format = "format";
    let name_format = tokens!(cfg, "name", format);
    let name_format_short = tokens!(cfg, ("nameformat", ["nf"]));
    let name_formatting = [
        command!(name_format => "cfg_name_format_show").help("Shows the name format"),
        command!(name_format, RESET => "cfg_name_format_reset").help("Resets the name format"),
        command!(name_format, ("format", OpaqueString) => "cfg_name_format_update")
            .help("Changes your system's username formatting"),
        command!(name_format_short => "cfg_name_format_show").help("Shows the name format"),
        command!(name_format_short, RESET => "cfg_name_format_reset")
            .help("Resets the name format"),
        command!(name_format_short, ("format", OpaqueString) => "cfg_name_format_update")
            .help("Changes your system's username formatting"),
    ];

    let server = "server";
    let server_name_format = tokens!(cfg, server, "name", format);
    let server_format = tokens!(
        cfg,
        ("server", ["servername"]),
        ("format", ["nameformat", "nf"])
    );
    let server_format_short = tokens!(
        cfg,
        ("snf", ["servernf", "servernameformat", "snameformat"])
    );
    let server_name_formatting = [
        command!(server_name_format => "cfg_server_name_format_show")
            .help("Shows the server name format"),
        command!(server_name_format, RESET => "cfg_server_name_format_reset")
            .help("Resets the server name format"),
        command!(server_name_format, ("format", OpaqueString) => "cfg_server_name_format_update")
            .help("Changes your system's username formatting in the current server"),
        command!(server_format => "cfg_server_name_format_show")
            .help("Shows the server name format"),
        command!(server_format, RESET => "cfg_server_name_format_reset")
            .help("Resets the server name format"),
        command!(server_format, ("format", OpaqueString) => "cfg_server_name_format_update")
            .help("Changes your system's username formatting in the current server"),
        command!(server_format_short => "cfg_server_name_format_show")
            .help("Shows the server name format"),
        command!(server_format_short, RESET => "cfg_server_name_format_reset")
            .help("Resets the server name format"),
        command!(server_format_short, ("format", OpaqueString) => "cfg_server_name_format_update")
            .help("Changes your system's username formatting in the current server"),
    ];

    let limit_ = ("limit", ["lim"]);
    let member_limit = tokens!(cfg, ("member", ["mem"]), limit_);
    let group_limit = tokens!(cfg, ("group", ["grp"]), limit_);
    let limit = tokens!(cfg, limit_);
    let limits = [
        command!(member_limit => "cfg_limits_update").help("Refreshes member/group limits"),
        command!(group_limit => "cfg_limits_update").help("Refreshes member/group limits"),
        command!(limit => "cfg_limits_update").help("Refreshes member/group limits"),
    ];

    base.into_iter()
        .chain(autoproxy)
        .chain(timezone)
        .chain(ping)
        .chain(privacy)
        .chain(private_info)
        .chain(proxy_settings)
        .chain(id_settings)
        .chain(color_settings)
        .chain(name_formatting)
        .chain(server_name_formatting)
        .chain(limits)
}
