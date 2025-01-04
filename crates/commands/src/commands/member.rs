use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    use Token::*;

    let member = ["member", "m"];
    let description = ["description", "desc"];
    let privacy = ["privacy", "priv"];
    let new = ["new", "n"];

    [
        command!(
            [member, new, MemberRef],
            "member_new",
            "Creates a new system member"
        ),
        command!(
            [member, MemberRef],
            "member_show",
            "Shows information about a member"
        ),
        command!(
            [member, MemberRef, description],
            "member_desc_show",
            "Shows a member's description"
        ),
        command!(
            [member, MemberRef, description, FullString],
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
                PrivacyLevel
            ],
            "member_privacy_update",
            "Changes a member's privacy settings"
        ),
    ]
    .into_iter()
}
