use std::{collections::HashSet, env, fmt::Write, fs, path::PathBuf, str::FromStr};

use command_parser::{
    parameter::{Parameter, ParameterKind},
    token::Token,
};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let write_location = env::args()
        .nth(1)
        .expect("file location should be provided");
    let write_location = PathBuf::from_str(&write_location).unwrap();

    let commands = command_definitions::all().collect::<Vec<_>>();

    let mut glue = String::new();

    writeln!(&mut glue, "#nullable enable\n")?;
    writeln!(&mut glue, "using PluralKit.Core;\n")?;
    writeln!(&mut glue, "using Myriad.Types;")?;
    writeln!(&mut glue, "namespace PluralKit.Bot;\n")?;

    let mut commands_seen = HashSet::new();
    let mut record_fields = String::new();
    for command in &commands {
        if commands_seen.contains(&command.cb) {
            continue;
        }
        writeln!(
            &mut record_fields,
            r#"public record {command_name}({command_name}Params parameters, {command_name}Flags flags): Commands;"#,
            command_name = command_callback_to_name(&command.cb),
        )?;
        commands_seen.insert(command.cb.clone());
    }

    commands_seen.clear();
    let mut match_branches = String::new();
    for command in &commands {
        if commands_seen.contains(&command.cb) {
            continue;
        }
        let mut command_params_init = String::new();
        let command_params = find_parameters(&command.tokens);
        for param in &command_params {
            writeln!(
                &mut command_params_init,
                r#"@{name} = await ctx.ParamResolve{extract_fn_name}("{name}"){throw_null},"#,
                name = param.name().replace("-", "_"),
                extract_fn_name = get_param_param_ty(param.kind()),
                throw_null = param
                    .is_optional()
                    .then_some("")
                    .unwrap_or(" ?? throw new PKError(\"this is a bug\")"),
            )?;
        }
        let mut command_flags_init = String::new();
        for flag in &command.flags {
            if let Some(param) = flag.get_value() {
                writeln!(
                    &mut command_flags_init,
                    r#"@{name} = await ctx.FlagResolve{extract_fn_name}("{name}"),"#,
                    name = flag.get_name().replace("-", "_"),
                    extract_fn_name = get_param_param_ty(param.kind()),
                )?;
            } else {
                writeln!(
                    &mut command_flags_init,
                    r#"@{name} = ctx.Parameters.HasFlag("{name}"),"#,
                    name = flag.get_name().replace("-", "_"),
                )?;
            }
        }
        write!(
            &mut match_branches,
            r#"
            "{command_callback}" => new {command_name}(
                new {command_name}Params {{ {command_params_init} }},
                new {command_name}Flags {{ {command_flags_init} }}
            ),
            "#,
            command_name = command_callback_to_name(&command.cb),
            command_callback = command.cb,
        )?;
        commands_seen.insert(command.cb.clone());
    }
    write!(
        &mut glue,
        r#"
        public abstract record Commands()
        {{
            {record_fields}

            public static async Task<Commands?> FromContext(Context ctx)
            {{
                return ctx.Parameters.Callback() switch
                {{
                    {match_branches}
                    _ => null,
                }};
            }}
        }}
        "#,
    )?;

    commands_seen.clear();
    for command in &commands {
        if commands_seen.contains(&command.cb) {
            continue;
        }
        let mut command_params_fields = String::new();
        let command_params = find_parameters(&command.tokens);
        for param in &command_params {
            writeln!(
                &mut command_params_fields,
                r#"public required {ty}{nullable} @{name};"#,
                name = param.name().replace("-", "_"),
                ty = get_param_ty(param.kind()),
                nullable = param.is_optional().then_some("?").unwrap_or(""),
            )?;
        }
        let mut command_flags_fields = String::new();
        for flag in &command.flags {
            if let Some(param) = flag.get_value() {
                writeln!(
                    &mut command_flags_fields,
                    r#"public {ty}? @{name};"#,
                    name = flag.get_name().replace("-", "_"),
                    ty = get_param_ty(param.kind()),
                )?;
            } else {
                writeln!(
                    &mut command_flags_fields,
                    r#"public required bool @{name};"#,
                    name = flag.get_name().replace("-", "_"),
                )?;
            }
        }
        let mut command_reply_format = String::new();
        if command
            .flags
            .iter()
            .any(|flag| flag.get_name() == "plaintext")
        {
            writeln!(
                &mut command_reply_format,
                r#"if (plaintext) return ReplyFormat.Plaintext;"#,
            )?;
        }
        if command.flags.iter().any(|flag| flag.get_name() == "raw") {
            writeln!(
                &mut command_reply_format,
                r#"if (raw) return ReplyFormat.Raw;"#,
            )?;
        }
        command_reply_format.push_str("return ReplyFormat.Standard;\n");
        let mut command_list_options = String::new();
        let mut command_list_options_class = String::new();
        let list_flags = command_definitions::utils::get_list_flags();
        if list_flags.iter().all(|flag| command.flags.contains(&flag)) {
            write!(&mut command_list_options_class, ": IHasListOptions")?;
            writeln!(
                &mut command_list_options,
                r#"
                public ListOptions GetListOptions(Context ctx, SystemId target)
                {{
                    var directLookupCtx = ctx.DirectLookupContextFor(target);
                    var lookupCtx = ctx.LookupContextFor(target);
                    
                    var p = new ListOptions();
                    p.Type = full ? ListType.Long : ListType.Short;
                    // Search description filter
                    p.SearchDescription = search_description;

                    // Sort property
                    if (by_name) p.SortProperty = SortProperty.Name;
                    if (by_display_name) p.SortProperty = SortProperty.DisplayName;
                    if (by_id) p.SortProperty = SortProperty.Hid;
                    if (by_message_count) p.SortProperty = SortProperty.MessageCount;
                    if (by_created) p.SortProperty = SortProperty.CreationDate;
                    if (by_last_fronted) p.SortProperty = SortProperty.LastSwitch;
                    if (by_last_message) p.SortProperty = SortProperty.LastMessage;
                    if (by_birthday) p.SortProperty = SortProperty.Birthdate;
                    if (random) p.SortProperty = SortProperty.Random;

                    // Sort reverse
                    p.Reverse = reverse;

                    // Privacy filter
                    if (all) p.PrivacyFilter = null;
                    if (private_only) p.PrivacyFilter = PrivacyLevel.Private;
                    // PERM CHECK: If we're trying to access non-public members of another system, error
                    if (p.PrivacyFilter != PrivacyLevel.Public && directLookupCtx != LookupContext.ByOwner)
                        // TODO: should this just return null instead of throwing or something? >.>
                        throw Errors.NotOwnInfo;
                    
                    // this is for searching
                    p.Context = lookupCtx;

                    // Additional fields to include
                    p.IncludeLastSwitch = with_last_switch;
                    p.IncludeLastMessage = with_last_message;
                    p.IncludeMessageCount = with_message_count;
                    p.IncludeCreated = with_created;
                    p.IncludeAvatar = with_avatar;
                    p.IncludePronouns = with_pronouns;
                    p.IncludeDisplayName = with_displayname;
                    p.IncludeBirthday = with_birthday;

                    // Always show the sort property (unless short list and already showing something else)
                    if (p.Type != ListType.Short || p.includedCount == 0)
                    {{
                        if (p.SortProperty == SortProperty.DisplayName) p.IncludeDisplayName = true;
                        if (p.SortProperty == SortProperty.MessageCount) p.IncludeMessageCount = true;
                        if (p.SortProperty == SortProperty.CreationDate) p.IncludeCreated = true;
                        if (p.SortProperty == SortProperty.LastSwitch) p.IncludeLastSwitch = true;
                        if (p.SortProperty == SortProperty.LastMessage) p.IncludeLastMessage = true;
                        if (p.SortProperty == SortProperty.Birthdate) p.IncludeBirthday = true;
                    }}

                    p.AssertIsValid();
                    return p;
                }}
                "#,
            )?;
        }
        write!(
            &mut glue,
            r#"
            public class {command_name}Params
            {{
                {command_params_fields}
            }}
            public class {command_name}Flags {command_list_options_class}
            {{
                {command_flags_fields}

                public ReplyFormat GetReplyFormat()
                {{
                    {command_reply_format}
                }}
                
                {command_list_options}
            }}
            "#,
            command_name = command_callback_to_name(&command.cb),
        )?;
        commands_seen.insert(command.cb.clone());
    }
    fs::write(write_location, glue)?;
    Ok(())
}

fn command_callback_to_name(cb: &str) -> String {
    cb.split("_")
        .map(|w| w.chars().nth(0).unwrap().to_uppercase().collect::<String>() + &w[1..])
        .collect()
}

fn get_param_ty(kind: ParameterKind) -> &'static str {
    match kind {
        ParameterKind::OpaqueString | ParameterKind::OpaqueStringRemainder => "string",
        ParameterKind::OpaqueInt => "int",
        ParameterKind::MemberRef => "PKMember",
        ParameterKind::MemberRefs => "List<PKMember>",
        ParameterKind::GroupRef => "PKGroup",
        ParameterKind::GroupRefs => "List<PKGroup>",
        ParameterKind::SystemRef => "PKSystem",
        ParameterKind::UserRef => "User",
        ParameterKind::MemberPrivacyTarget => "MemberPrivacySubject",
        ParameterKind::GroupPrivacyTarget => "GroupPrivacySubject",
        ParameterKind::SystemPrivacyTarget => "SystemPrivacySubject",
        ParameterKind::PrivacyLevel => "PrivacyLevel",
        ParameterKind::Toggle => "bool",
        ParameterKind::Avatar => "ParsedImage",
        ParameterKind::MessageRef => "Message.Reference",
        ParameterKind::ChannelRef => "Channel",
        ParameterKind::GuildRef => "Guild",
        ParameterKind::ProxySwitchAction => "SystemConfig.ProxySwitchAction",
    }
}

fn get_param_param_ty(kind: ParameterKind) -> &'static str {
    match kind {
        ParameterKind::OpaqueString | ParameterKind::OpaqueStringRemainder => "Opaque",
        ParameterKind::OpaqueInt => "Number",
        ParameterKind::MemberRef => "Member",
        ParameterKind::MemberRefs => "Members",
        ParameterKind::GroupRef => "Group",
        ParameterKind::GroupRefs => "Groups",
        ParameterKind::SystemRef => "System",
        ParameterKind::UserRef => "User",
        ParameterKind::MemberPrivacyTarget => "MemberPrivacyTarget",
        ParameterKind::GroupPrivacyTarget => "GroupPrivacyTarget",
        ParameterKind::SystemPrivacyTarget => "SystemPrivacyTarget",
        ParameterKind::PrivacyLevel => "PrivacyLevel",
        ParameterKind::Toggle => "Toggle",
        ParameterKind::Avatar => "Avatar",
        ParameterKind::MessageRef => "Message",
        ParameterKind::ChannelRef => "Channel",
        ParameterKind::GuildRef => "Guild",
        ParameterKind::ProxySwitchAction => "ProxySwitchAction",
    }
}

fn find_parameters(tokens: &[Token]) -> Vec<&Parameter> {
    let mut result = Vec::new();
    for token in tokens {
        match token {
            Token::Parameter(param) => result.push(param),
            _ => {}
        }
    }
    result
}
