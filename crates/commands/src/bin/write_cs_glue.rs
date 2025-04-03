use std::{env, fmt::Write, fs, path::PathBuf, str::FromStr};

use command_parser::{
    command, parameter::{Parameter, ParameterKind}, token::Token
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
    writeln!(&mut glue, "namespace PluralKit.Bot;\n")?;

    let mut record_fields = String::new();
    for command in &commands {
        writeln!(
            &mut record_fields,
            r#"public record {command_name}({command_name}Params parameters, {command_name}Flags flags): Commands;"#,
            command_name = command_callback_to_name(&command.cb),
        )?;
    }
    let mut match_branches = String::new();
    for command in &commands {
        let mut command_params_init = String::new();
        let command_params = find_parameters(&command.tokens);
        for param in &command_params {
            writeln!(
                &mut command_params_init,
                r#"@{name} = await ctx.ParamResolve{extract_fn_name}("{name}") ?? throw new PKError("this is a bug"),"#,
                name = param.name(),
                extract_fn_name = get_param_param_ty(param.kind()),
            )?;
        }
        let mut command_flags_init = String::new();
        for flag in &command.flags {
            if let Some(kind) = flag.get_value() {
                writeln!(
                    &mut command_flags_init,
                    r#"@{name} = await ctx.FlagResolve{extract_fn_name}("{name}"),"#,
                    name = flag.get_name(),
                    extract_fn_name = get_param_param_ty(kind),
                )?;
            } else {
                writeln!(
                    &mut command_flags_init,
                    r#"@{name} = ctx.Parameters.HasFlag("{name}"),"#,
                    name = flag.get_name(),
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
    for command in &commands {
        let mut command_params_fields = String::new();
        let command_params = find_parameters(&command.tokens);
        for param in &command_params {
            writeln!(
                &mut command_params_fields,
                r#"public required {ty} @{name};"#,
                name = param.name(),
                ty = get_param_ty(param.kind()),
            )?;
        }
        let mut command_flags_fields = String::new();
        for flag in &command.flags {
            if let Some(kind) = flag.get_value() {
                writeln!(
                    &mut command_flags_fields,
                    r#"public {ty}? @{name};"#,
                    name = flag.get_name(),
                    ty = get_param_ty(kind),
                )?;
            } else {
                writeln!(
                    &mut command_flags_fields,
                    r#"public required bool @{name};"#,
                    name = flag.get_name(),
                )?;
            }
        }
        let mut command_reply_format = String::new();
        if command.flags.iter().any(|flag| flag.get_name() == "plaintext") {
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
        write!(
            &mut glue,
            r#"
            public class {command_name}Params
            {{
                {command_params_fields}
            }}
            public class {command_name}Flags
            {{
                {command_flags_fields}

                public ReplyFormat GetReplyFormat()
                {{
                    {command_reply_format}
                }}
            }}
            "#,
            command_name = command_callback_to_name(&command.cb),
        )?;
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
        ParameterKind::MemberRef => "PKMember",
        ParameterKind::SystemRef => "PKSystem",
        ParameterKind::MemberPrivacyTarget => "MemberPrivacySubject",
        ParameterKind::PrivacyLevel => "string",
        ParameterKind::Toggle => "bool",
        ParameterKind::Avatar => "ParsedImage",
    }
}

fn get_param_param_ty(kind: ParameterKind) -> &'static str {
    match kind {
        ParameterKind::OpaqueString | ParameterKind::OpaqueStringRemainder => "Opaque",
        ParameterKind::MemberRef => "Member",
        ParameterKind::SystemRef => "System",
        ParameterKind::MemberPrivacyTarget => "MemberPrivacyTarget",
        ParameterKind::PrivacyLevel => "PrivacyLevel",
        ParameterKind::Toggle => "Toggle",
        ParameterKind::Avatar => "Avatar",
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
