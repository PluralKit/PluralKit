use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    let member = ["member", "m"];
    let description = ["description", "desc"];
    let privacy = ["privacy", "priv"];
    let new = ["new", "n"];

    let member_target = tokens!(member, MemberRef);
    let member_desc = concat_tokens!(member_target, [description]);
    let member_privacy = concat_tokens!(member_target, [privacy]);

    [
        command!([member, new, ("name", OpaqueString)] => "member_new")
            .help("Creates a new system member"),
        command!(member_target => "member_show")
            .flag("pt")
            .help("Shows information about a member"),
        command!(member_desc => "member_desc_show").help("Shows a member's description"),
        command!(member_desc, ("description", OpaqueStringRemainder) => "member_desc_update")
            .help("Changes a member's description"),
        command!(member_privacy => "member_privacy_show")
            .help("Displays a member's current privacy settings"),
        command!(
            member_privacy, MemberPrivacyTarget, ("new_privacy_level", PrivacyLevel)
            => "member_privacy_update"
        )
        .help("Changes a member's privacy settings"),
        command!(member_target, "soulscream" => "member_soulscream").show_in_suggestions(false),
    ]
    .into_iter()
}
