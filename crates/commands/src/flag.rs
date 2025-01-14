use std::fmt::Display;

use smol_str::SmolStr;

use crate::Parameter;

#[derive(Debug, Clone)]
pub enum FlagValue {
    OpaqueString,
}

impl FlagValue {
    fn try_match(&self, input: &str) -> Result<Parameter, FlagValueMatchError> {
        if input.is_empty() {
            return Err(FlagValueMatchError::ValueMissing);
        }

        match self {
            Self::OpaqueString => Ok(Parameter::OpaqueString { raw: input.into() }),
        }
    }
}

impl Display for FlagValue {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            FlagValue::OpaqueString => write!(f, "value"),
        }
    }
}

#[derive(Debug)]
pub enum FlagValueMatchError {
    ValueMissing,
}

#[derive(Debug, Clone)]
pub struct Flag {
    name: SmolStr,
    value: Option<FlagValue>,
}

impl Display for Flag {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "-{}", self.name)?;
        if let Some(value) = self.value.as_ref() {
            write!(f, "={value}")?;
        }
        Ok(())
    }
}

#[derive(Debug)]
pub enum FlagMatchError {
    ValueMatchFailed(FlagValueMatchError),
}

type TryMatchFlagResult = Option<Result<Option<Parameter>, FlagMatchError>>;

impl Flag {
    pub fn new(name: impl Into<SmolStr>) -> Self {
        Self {
            name: name.into(),
            value: None,
        }
    }

    pub fn with_value(mut self, value: FlagValue) -> Self {
        self.value = Some(value);
        self
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn value(&self) -> Option<&FlagValue> {
        self.value.as_ref()
    }

    pub fn try_match(&self, input_name: &str, input_value: Option<&str>) -> TryMatchFlagResult {
        // if not matching flag then skip anymore matching
        if self.name != input_name {
            return None;
        }
        // get token to try matching with, if flag doesn't have one then that means it is matched (it is without any value)
        let Some(value) = self.value() else {
            return Some(Ok(None));
        };
        // check if we have a non-empty flag value, we return error if not (because flag requested a value)
        let Some(input_value) = input_value else {
            return Some(Err(FlagMatchError::ValueMatchFailed(
                FlagValueMatchError::ValueMissing,
            )));
        };
        // try matching the value
        match value.try_match(input_value) {
            Ok(param) => Some(Ok(Some(param))),
            Err(err) => Some(Err(FlagMatchError::ValueMatchFailed(err))),
        }
    }
}
