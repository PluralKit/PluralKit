use std::{fmt::Display, sync::Arc};

use smol_str::SmolStr;

use crate::{parameter::Parameter, Parameter as FfiParam};

#[derive(Debug)]
pub enum FlagValueMatchError {
    ValueMissing,
    InvalidValue { raw: SmolStr, msg: SmolStr },
}

#[derive(Debug, Clone)]
pub struct Flag {
    name: SmolStr,
    value: Option<Arc<dyn Parameter>>,
}

impl Display for Flag {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "-{}", self.name)?;
        if let Some(value) = self.value.as_ref() {
            write!(f, "=")?;
            value.format(f, value.default_name())?;
        }
        Ok(())
    }
}

#[derive(Debug)]
pub enum FlagMatchError {
    ValueMatchFailed(FlagValueMatchError),
}

type TryMatchFlagResult = Option<Result<Option<FfiParam>, FlagMatchError>>;

impl Flag {
    pub fn new(name: impl Into<SmolStr>) -> Self {
        Self {
            name: name.into(),
            value: None,
        }
    }

    pub fn with_value(mut self, param: impl Parameter + 'static) -> Self {
        self.value = Some(Arc::new(param));
        self
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn try_match(&self, input_name: &str, input_value: Option<&str>) -> TryMatchFlagResult {
        // if not matching flag then skip anymore matching
        if self.name != input_name {
            return None;
        }
        // get token to try matching with, if flag doesn't have one then that means it is matched (it is without any value)
        let Some(value) = self.value.as_deref() else {
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
