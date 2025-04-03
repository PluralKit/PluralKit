use std::fmt::Display;

use smol_str::SmolStr;

use crate::parameter::{Parameter, ParameterKind, ParameterValue};

#[derive(Debug)]
pub enum FlagValueMatchError {
    ValueMissing,
    InvalidValue { raw: SmolStr, msg: SmolStr },
}

#[derive(Debug, Clone)]
pub struct Flag {
    name: SmolStr,
    aliases: Vec<SmolStr>,
    value: Option<ParameterKind>,
}

impl Display for Flag {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "-{}", self.name)?;
        if let Some(value) = self.value.as_ref() {
            write!(f, "=")?;
            Parameter::from(*value).fmt(f)?;
        }
        Ok(())
    }
}

#[derive(Debug)]
pub enum FlagMatchError {
    ValueMatchFailed(FlagValueMatchError),
}

type TryMatchFlagResult = Option<Result<Option<ParameterValue>, FlagMatchError>>;

impl Flag {
    pub fn new(name: impl Into<SmolStr>) -> Self {
        Self {
            name: name.into(),
            aliases: Vec::new(),
            value: None,
        }
    }

    pub fn value(mut self, param: ParameterKind) -> Self {
        self.value = Some(param);
        self
    }

    pub fn alias(mut self, alias: impl Into<SmolStr>) -> Self {
        self.aliases.push(alias.into());
        self
    }

    pub fn get_name(&self) -> &str {
        &self.name
    }

    pub fn get_value(&self) -> Option<ParameterKind> {
        self.value
    }

    pub fn get_aliases(&self) -> impl Iterator<Item = &str> {
        self.aliases.iter().map(|s| s.as_str())
    }

    pub fn try_match(&self, input_name: &str, input_value: Option<&str>) -> TryMatchFlagResult {
        // if not matching the name or any aliases then skip anymore matching
        if self.name != input_name && self.get_aliases().all(|s| s.ne(input_name)) {
            return None;
        }
        // get token to try matching with, if flag doesn't have one then that means it is matched (it is without any value)
        let Some(value) = self.value.as_ref() else {
            return Some(Ok(None));
        };
        // check if we have a non-empty flag value, we return error if not (because flag requested a value)
        let Some(input_value) = input_value else {
            return Some(Err(FlagMatchError::ValueMatchFailed(
                FlagValueMatchError::ValueMissing,
            )));
        };
        // try matching the value
        match value.match_value(input_value) {
            Ok(param) => Some(Ok(Some(param))),
            Err(err) => Some(Err(FlagMatchError::ValueMatchFailed(
                FlagValueMatchError::InvalidValue {
                    raw: input_value.into(),
                    msg: err,
                },
            ))),
        }
    }
}

impl From<&str> for Flag {
    fn from(name: &str) -> Self {
        Flag::new(name)
    }
}

impl From<(&str, ParameterKind)> for Flag {
    fn from((name, value): (&str, ParameterKind)) -> Self {
        Flag::new(name).value(value)
    }
}

impl<const L: usize> From<(&str, [&str; L])> for Flag {
    fn from((name, aliases): (&str, [&str; L])) -> Self {
        let mut flag = Flag::new(name);
        for alias in aliases {
            flag = flag.alias(alias);
        }
        flag
    }
}

impl<const L: usize> From<((&str, [&str; L]), ParameterKind)> for Flag {
    fn from(((name, aliases), value): ((&str, [&str; L]), ParameterKind)) -> Self {
        Flag::from((name, aliases)).value(value)
    }
}
