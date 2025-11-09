use command_parser::parameter;

use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let cfg = ("config", ["cfg", "configure"]);
    let ap = tokens!(cfg, ("autoproxy", ["ap"]));

    let ap_account = tokens!(ap, ("account", ["ac"]));
    let ap_timeout = tokens!(ap, ("timeout", ["tm"]));

    let timezone = tokens!(cfg, ("timezone", ["zone", "tz"]));
    let ping = tokens!(cfg, ("ping", ["ping"]));

    let priv_ = ("private", ["priv"]);
    let member_privacy = tokens!(cfg, priv_, ("member", ["mem"]));
    let member_privacy_short = tokens!(cfg, ("mp", ["mp"]));
    let group_privacy = tokens!(cfg, priv_, ("group", ["grp"]));
    let group_privacy_short = tokens!(cfg, ("gp", ["gp"]));

    let show = ("show", ["show"]);
    let show_private = tokens!(cfg, show, priv_);
    let show_private_short = tokens!(cfg, ("sp", ["sp"]));

    let proxy = ("proxy", ["px"]);
    let proxy_case = tokens!(cfg, proxy, ("case", ["caps", "capitalize", "capitalise"]));
    let proxy_error = tokens!(cfg, proxy, ("error", ["errors"]));
    let proxy_error_short = tokens!(cfg, ("pe", ["pe"]));

    let id = ("id", ["ids"]);
    let split_id = tokens!(cfg, ("split", ["split"]), id);
    let split_id_short = tokens!(cfg, ("sid", ["sid", "sids"]));
    let cap_id = tokens!(cfg, ("cap", ["caps", "capitalize", "capitalise"]), id);
    let cap_id_short = tokens!(cfg, ("capid", ["capid", "capids"]));

    let pad = ("pad", ["padding"]);
    let pad_id = tokens!(cfg, pad, id);
    let id_pad = tokens!(cfg, id, pad);
    let id_pad_short = tokens!(cfg, ("idpad", ["idpad", "padid", "padids"]));

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

    let proxy_switch = tokens!(cfg, ("proxy", ["proxy"]), ("switch", ["switch"]));
    let proxy_switch_short = tokens!(cfg, ("proxyswitch", ["proxyswitch", "ps"]));

    let format = ("format", ["format"]);
    let name_format = tokens!(cfg, ("name", ["name"]), format);
    let name_format_short = tokens!(cfg, ("nameformat", ["nameformat", "nf"]));

    let server = ("server", ["server"]);
    let server_name_format = tokens!(cfg, server, ("name", ["name"]), format);
    let server_format = tokens!(
        cfg,
        ("server", ["server", "servername"]),
        ("format", ["format", "nameformat", "nf"])
    );
    let server_format_short = tokens!(
        cfg,
        (
            "snf",
            ["snf", "servernf", "servernameformat", "snameformat"]
        )
    );

    let limit_ = ("limit", ["limit", "lim"]);
    let member_limit = tokens!(cfg, ("member", ["mem"]), limit_);
    let group_limit = tokens!(cfg, ("group", ["grp"]), limit_);
    let limit = tokens!(cfg, limit_);

    [
        command!(cfg => "cfg_show").help("Shows the current configuration"),
        command!(ap_account => "cfg_ap_account_show")
            .help("Shows autoproxy status for the account"),
        command!(ap_account, Toggle => "cfg_ap_account_update")
            .help("Toggles autoproxy for the account"),
        command!(ap_timeout => "cfg_ap_timeout_show").help("Shows the autoproxy timeout"),
        command!(ap_timeout, RESET => "cfg_ap_timeout_reset").help("Resets the autoproxy timeout"),
        command!(ap_timeout, parameter::Toggle::Off => "cfg_ap_timeout_off")
            .help("Disables the autoproxy timeout"),
        command!(ap_timeout, ("timeout", OpaqueString) => "cfg_ap_timeout_update")
            .help("Sets the autoproxy timeout"),
        command!(timezone => "cfg_timezone_show").help("Shows the system timezone"),
        command!(timezone, RESET => "cfg_timezone_reset").help("Resets the system timezone"),
        command!(timezone, ("timezone", OpaqueString) => "cfg_timezone_update")
            .help("Sets the system timezone"),
        command!(ping => "cfg_ping_show").help("Shows the ping setting"),
        command!(ping, Toggle => "cfg_ping_update").help("Updates the ping setting"),
        command!(member_privacy => "cfg_member_privacy_show")
            .help("Shows the default privacy for new members"),
        command!(member_privacy, Toggle => "cfg_member_privacy_update")
            .help("Sets the default privacy for new members"),
        command!(member_privacy_short => "cfg_member_privacy_show")
            .help("Shows the default privacy for new members"),
        command!(member_privacy_short, Toggle => "cfg_member_privacy_update")
            .help("Sets the default privacy for new members"),
        command!(group_privacy => "cfg_group_privacy_show")
            .help("Shows the default privacy for new groups"),
        command!(group_privacy, Toggle => "cfg_group_privacy_update")
            .help("Sets the default privacy for new groups"),
        command!(group_privacy_short => "cfg_group_privacy_show")
            .help("Shows the default privacy for new groups"),
        command!(group_privacy_short, Toggle => "cfg_group_privacy_update")
            .help("Sets the default privacy for new groups"),
        command!(show_private => "cfg_show_private_info_show")
            .help("Shows whether private info is shown"),
        command!(show_private, Toggle => "cfg_show_private_info_update")
            .help("Toggles showing private info"),
        command!(show_private_short => "cfg_show_private_info_show")
            .help("Shows whether private info is shown"),
        command!(show_private_short, Toggle => "cfg_show_private_info_update")
            .help("Toggles showing private info"),
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
        command!(show_color => "cfg_card_show_color_hex_show")
            .help("Shows whether color hex codes are shown on cards"),
        command!(show_color, Toggle => "cfg_card_show_color_hex_update")
            .help("Toggles showing color hex codes on cards"),
        command!(show_color_short => "cfg_card_show_color_hex_show")
            .help("Shows whether color hex codes are shown on cards"),
        command!(show_color_short, Toggle => "cfg_card_show_color_hex_update")
            .help("Toggles showing color hex codes on cards"),
        command!(proxy_switch => "cfg_proxy_switch_show").help("Shows the proxy switch behavior"),
        command!(proxy_switch, ProxySwitchAction => "cfg_proxy_switch_update")
            .help("Sets the proxy switch behavior"),
        command!(proxy_switch_short => "cfg_proxy_switch_show")
            .help("Shows the proxy switch behavior"),
        command!(proxy_switch_short, ProxySwitchAction => "cfg_proxy_switch_update")
            .help("Sets the proxy switch behavior"),
        command!(name_format => "cfg_name_format_show").help("Shows the name format"),
        command!(name_format, RESET => "cfg_name_format_reset").help("Resets the name format"),
        command!(name_format, ("format", OpaqueString) => "cfg_name_format_update")
            .help("Sets the name format"),
        command!(name_format_short => "cfg_name_format_show").help("Shows the name format"),
        command!(name_format_short, RESET => "cfg_name_format_reset")
            .help("Resets the name format"),
        command!(name_format_short, ("format", OpaqueString) => "cfg_name_format_update")
            .help("Sets the name format"),
        command!(server_name_format => "cfg_server_name_format_show")
            .help("Shows the server name format"),
        command!(server_name_format, RESET => "cfg_server_name_format_reset")
            .help("Resets the server name format"),
        command!(server_name_format, ("format", OpaqueString) => "cfg_server_name_format_update")
            .help("Sets the server name format"),
        command!(server_format => "cfg_server_name_format_show")
            .help("Shows the server name format"),
        command!(server_format, RESET => "cfg_server_name_format_reset")
            .help("Resets the server name format"),
        command!(server_format, ("format", OpaqueString) => "cfg_server_name_format_update")
            .help("Sets the server name format"),
        command!(server_format_short => "cfg_server_name_format_show")
            .help("Shows the server name format"),
        command!(server_format_short, RESET => "cfg_server_name_format_reset")
            .help("Resets the server name format"),
        command!(server_format_short, ("format", OpaqueString) => "cfg_server_name_format_update")
            .help("Sets the server name format"),
        command!(member_limit => "cfg_limits_update").help("Refreshes member/group limits"),
        command!(group_limit => "cfg_limits_update").help("Refreshes member/group limits"),
        command!(limit => "cfg_limits_update").help("Refreshes member/group limits"),
    ]
    .into_iter()
}
