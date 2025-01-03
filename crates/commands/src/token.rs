use smol_str::SmolStr;

#[derive(Debug, Clone, Eq, Hash, PartialEq)]
pub enum Token {
    /// Token used to represent a finished command (i.e. no more parameters required)
    // todo: this is likely not the right way to represent this
    Empty,

    /// A bot-defined value ("member" in `pk;member MyName`)
    Value(Vec<SmolStr>),
    /// A command defined by multiple values
    // todo!
    MultiValue(Vec<Vec<SmolStr>>),

    FullString,

    /// Member reference (hid or member name)
    MemberRef,
    MemberPrivacyTarget,

    PrivacyLevel,

    // currently not included in command definitions
    // todo: flags with values
    Flag,
}

pub enum TokenMatchResult {
    NoMatch,
    /// Token matched, optionally with a value.
    Match(Option<SmolStr>),
}

// move this somewhere else
lazy_static::lazy_static!(
    static ref MEMBER_PRIVACY_TARGETS: Vec<SmolStr> = [
        "visibility",
        "name",
        "todo",
    ].into_iter().map(SmolStr::new_static).collect();
);

impl Token {
    pub fn cmd(value: impl Into<SmolStr>) -> Self {
        Self::Value(vec![value.into()])
    }

    pub fn cmd_with_alias(value: impl IntoIterator<Item = impl Into<SmolStr>>) -> Self {
        Self::Value(value.into_iter().map(Into::into).collect())
    }

    pub fn try_match(&self, input: Option<SmolStr>) -> TokenMatchResult {
        // short circuit on empty things
        if matches!(self, Self::Empty) && input.is_none() {
            return TokenMatchResult::Match(None);
        } else if input.is_none() {
            return TokenMatchResult::NoMatch;
        }

        let input = input.unwrap();

        // try actually matching stuff
        match self {
            Self::Empty => return TokenMatchResult::NoMatch,
            Self::Flag => unreachable!(), // matched upstream
            Self::Value(values) => {
                for v in values {
                    if input.trim() == v {
                        // c# bot currently needs subcommands provided as arguments
                        // todo!: remove this
                        return TokenMatchResult::Match(Some(v.clone()));
                    }
                }
            }
            Self::MultiValue(_) => todo!(),
            Self::FullString => return TokenMatchResult::Match(Some(input)),
            Self::MemberRef => return TokenMatchResult::Match(Some(input)),
            Self::MemberPrivacyTarget
                if MEMBER_PRIVACY_TARGETS
                    .iter()
                    .any(|target| target.eq(input.trim())) =>
            {
                return TokenMatchResult::Match(Some(input))
            }
            Self::MemberPrivacyTarget => {}
            Self::PrivacyLevel if input == "public" || input == "private" => {
                return TokenMatchResult::Match(Some(input))
            }
            Self::PrivacyLevel => {}
        }
        // note: must not add a _ case to the above match
        // instead, for conditional matches, also add generic cases with no return

        return TokenMatchResult::NoMatch;
    }
}
