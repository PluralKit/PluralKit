use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    use Token::*;

    let member = ["member", "m"];
    let description = ["description", "desc"];
    let privacy = ["privacy", "priv"];
    let new = ["new", "n"];

    [
        command!(
            [member, new, OpaqueString("name")],
            "member_new",
            "Creates a new system member"
        ),
        command!(
            [member, MemberRef("target")],
            "member_show",
            "Shows information about a member"
        )
        .value_flag("pt", FlagValue::OpaqueString),
        command!(
            [member, MemberRef("target"), description],
            "member_desc_show",
            "Shows a member's description"
        ),
        command!(
            [
                member,
                MemberRef("target"),
                description,
                OpaqueRemainder("description")
            ],
            "member_desc_update",
            "Changes a member's description"
        ),
        command!(
            [member, MemberRef("target"), privacy],
            "member_privacy_show",
            "Displays a member's current privacy settings"
        ),
        command!(
            [
                member,
                MemberRef("target"),
                privacy,
                MemberPrivacyTarget("privacy_target"),
                PrivacyLevel("new_privacy_level")
            ],
            "member_privacy_update",
            "Changes a member's privacy settings"
        ),
        command!(
            [member, MemberRef("target"), "soulscream"],
            "member_soulscream",
            "todo"
        )
        .show_in_suggestions(false),
    ]
    .into_iter()
}
