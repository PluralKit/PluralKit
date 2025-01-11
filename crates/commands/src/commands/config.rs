use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    use Token::*;

    let cfg = ["config", "cfg"];
    let autoproxy = ["autoproxy", "ap"];

    [
        command!(
            [cfg, autoproxy, ["account", "ac"]],
            "cfg_ap_account_show",
            "Shows autoproxy status for the account"
        ),
        command!(
            [cfg, autoproxy, ["account", "ac"], Toggle("toggle")],
            "cfg_ap_account_update",
            "Toggles autoproxy for the account"
        ),
        command!(
            [cfg, autoproxy, ["timeout", "tm"]],
            "cfg_ap_timeout_show",
            "Shows the autoproxy timeout"
        ),
        command!(
            [
                cfg,
                autoproxy,
                ["timeout", "tm"],
                [Disable("toggle"), Reset("reset"), FullString("timeout")] // todo: we should parse duration / time values
            ],
            "cfg_ap_timeout_update",
            "Sets the autoproxy timeout"
        ),
    ]
    .into_iter()
}
