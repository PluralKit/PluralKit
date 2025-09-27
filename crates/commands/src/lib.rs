use std::collections::HashMap;

use command_parser::{parameter::ParameterValue, Tree};

uniffi::include_scaffolding!("commands");

lazy_static::lazy_static! {
    pub static ref COMMAND_TREE: Tree = {
        let mut tree = Tree::default();

        command_definitions::all().into_iter().for_each(|x| tree.register_command(x));

        tree
    };
}

#[derive(Debug)]
pub enum CommandResult {
    Ok { command: ParsedCommand },
    Err { error: String },
}

#[derive(Debug, Clone)]
pub enum Parameter {
    MemberRef { member: String },
    MemberRefs { members: Vec<String> },
    GroupRef { group: String },
    SystemRef { system: String },
    GuildRef { guild: String },
    MemberPrivacyTarget { target: String },
    SystemPrivacyTarget { target: String },
    PrivacyLevel { level: String },
    OpaqueString { raw: String },
    Toggle { toggle: bool },
    Avatar { avatar: String },
}

impl From<ParameterValue> for Parameter {
    fn from(value: ParameterValue) -> Self {
        match value {
            ParameterValue::MemberRef(member) => Self::MemberRef { member },
            ParameterValue::MemberRefs(members) => Self::MemberRefs { members },
            ParameterValue::GroupRef(group) => Self::GroupRef { group },
            ParameterValue::SystemRef(system) => Self::SystemRef { system },
            ParameterValue::MemberPrivacyTarget(target) => Self::MemberPrivacyTarget { target },
            ParameterValue::SystemPrivacyTarget(target) => Self::SystemPrivacyTarget { target },
            ParameterValue::PrivacyLevel(level) => Self::PrivacyLevel { level },
            ParameterValue::OpaqueString(raw) => Self::OpaqueString { raw },
            ParameterValue::Toggle(toggle) => Self::Toggle { toggle },
            ParameterValue::Avatar(avatar) => Self::Avatar { avatar },
            ParameterValue::GuildRef(guild) => Self::GuildRef { guild },
        }
    }
}

#[derive(Debug)]
pub struct ParsedCommand {
    pub command_ref: String,
    pub params: HashMap<String, Parameter>,
    pub flags: HashMap<String, Option<Parameter>>,
}

pub fn parse_command(prefix: String, input: String) -> CommandResult {
    command_parser::parse_command(COMMAND_TREE.clone(), prefix, input).map_or_else(
        |error| CommandResult::Err { error },
        |parsed| CommandResult::Ok {
            command: {
                let command_ref = parsed.command_def.cb.into();
                let mut flags = HashMap::with_capacity(parsed.flags.capacity());
                for (name, value) in parsed.flags {
                    flags.insert(name, value.map(Parameter::from));
                }
                let mut params = HashMap::with_capacity(parsed.parameters.capacity());
                for (name, value) in parsed.parameters {
                    params.insert(name, Parameter::from(value));
                }
                ParsedCommand {
                    command_ref,
                    flags,
                    params,
                }
            },
        },
    )
}
