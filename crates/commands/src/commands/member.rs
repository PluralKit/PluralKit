use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let member = ["member", "m"];
    let description = ["description", "desc"];
    let privacy = ["privacy", "priv"];
    let new = ["new", "n"];

    [
        command!(
            [member, new, ("name", OpaqueString::SINGLE)],
            "member_new",
            "Creates a new system member"
        ),
        command!(
            [member, MemberRef],
            "member_show",
            "Shows information about a member"
        )
        .value_flag("pt", Disable),
        command!(
            [member, MemberRef, description],
            "member_desc_show",
            "Shows a member's description"
        ),
        command!(
            [
                member,
                MemberRef,
                description,
                ("description", OpaqueString::REMAINDER)
            ],
            "member_desc_update",
            "Changes a member's description"
        ),
        command!(
            [member, MemberRef, privacy],
            "member_privacy_show",
            "Displays a member's current privacy settings"
        ),
        command!(
            [
                member,
                MemberRef,
                privacy,
                MemberPrivacyTarget,
                ("new_privacy_level", PrivacyLevel)
            ],
            "member_privacy_update",
            "Changes a member's privacy settings"
        ),
        command!(
            [member, MemberRef, "soulscream"],
            "member_soulscream",
            "todo"
        )
        .show_in_suggestions(false),
    ]
    .into_iter()
}
