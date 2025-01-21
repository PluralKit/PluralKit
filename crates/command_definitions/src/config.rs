use command_parser::parameter;

use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let ap = tokens!(["config", "cfg"], ["autoproxy", "ap"]);

    let ap_account = concat_tokens!(ap, [["account", "ac"]]);
    let ap_timeout = concat_tokens!(ap, [["timeout", "tm"]]);

    [
        command!(ap_account => "cfg_ap_account_show")
            .help("Shows autoproxy status for the account"),
        command!(ap_account, Toggle => "cfg_ap_account_update")
            .help("Toggles autoproxy for the account"),
        command!(ap_timeout => "cfg_ap_timeout_show").help("Shows the autoproxy timeout"),
        command!(ap_timeout, parameter::RESET => "cfg_ap_timeout_reset")
            .help("Resets the autoproxy timeout"),
        command!(ap_timeout, parameter::DISABLE => "cfg_ap_timeout_off")
            .help("Disables the autoproxy timeout"),
        command!(ap_timeout, ("timeout", OpaqueString) => "cfg_ap_timeout_update")
            .help("Sets the autoproxy timeout"),
    ]
    .into_iter()
}
