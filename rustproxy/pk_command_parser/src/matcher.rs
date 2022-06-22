use std::ops::Range;

use crate::tokenizer::{Token, Tokenizer};

#[derive(Debug)]
pub enum Segment {
    Word(Vec<String>),
    Parameter { name: String, optional: bool },
}

#[derive(Debug)]
pub struct Pattern {
    segments: Vec<Segment>,
}

#[derive(Debug, PartialEq)]
pub struct ParameterMatch {
    name: String,
    value: String,
    span: Range<usize>,
}

#[derive(Debug, PartialEq)]
pub struct FlagMatch {
    name: String,
    value: Option<String>,
}

#[derive(Debug, PartialEq)]
pub struct MatchResult {
    parameters: Vec<ParameterMatch>,
    flags: Vec<FlagMatch>,
    remainder: Option<String>,
}

pub fn does_match(s: &str, pat: &Pattern) -> Option<MatchResult> {
    let mut flags = Vec::new();
    let mut parameters = Vec::new();

    let mut remainder_pos = None;

    let mut segments = pat.segments.iter().peekable();
    let mut tokenizer = Tokenizer::new(s);

    // loop until we find a keyword token
    while let Some(token) = tokenizer.next() {
        match token {
            Token::Flag { name, .. } => {
                // flags are set aside
                flags.push(FlagMatch { name, value: None });
            }
            Token::Keyword {
                value,
                quoted: _,
                span,
            } => {
                let mut next_segment = segments.next();

                match next_segment {
                    Some(Segment::Word(options)) => {
                        // keyword doesn't match? definitely not a match then
                        if !matches_word(&value, &options) {
                            return None;
                        }
                    }
                    Some(Segment::Parameter { name, optional }) => {
                        // for an optional parameter, check the next token instead and consume
                        if let Some(Segment::Word(options)) = segments.peek() {
                            if *optional {
                                if !matches_word(&value, &options) {
                                    return None;
                                }

                                segments.next();
                                continue;
                            }
                        }

                        // set parameter aside for later
                        parameters.push(ParameterMatch {
                            name: name.clone(),
                            span: span,
                            value: value.clone(),
                        });
                    }
                    None => {
                        // out of segments to match, but we already consumed the next token
                        // so set position aside for remainder and exit
                        remainder_pos = Some(span.start);
                        break;
                    }
                }
            }
        }
    }

    Some(MatchResult {
        parameters,
        flags,
        remainder: remainder_pos.map(|x| s[x..].to_string()),
    })
}

fn matches_word(word: &str, options: &[String]) -> bool {
    options.iter().any(|o| o.eq_ignore_ascii_case(word))
}

struct PatternBuilder {
    segments: Vec<Segment>,
}

impl PatternBuilder {
    fn new() -> PatternBuilder {
        PatternBuilder {
            segments: Vec::new(),
        }
    }

    fn word(mut self, options: &[&str]) -> PatternBuilder {
        self.segments.push(Segment::Word(
            options.iter().map(|x| x.to_string()).collect(),
        ));
        self
    }

    fn param(mut self, name: &str) -> PatternBuilder {
        self.segments.push(Segment::Parameter {
            name: name.to_string(),
            optional: false,
        });
        self
    }

    fn param_opt(mut self, name: &str) -> PatternBuilder {
        self.segments.push(Segment::Parameter {
            name: name.to_string(),
            optional: true,
        });
        self
    }

    fn build(self) -> Pattern {
        Pattern {
            segments: self.segments,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn hi() {
        let pat = PatternBuilder::new()
            .word(&["m", "member"])
            .param("member_ref")
            .word(&["desc", "d"])
            .build();

        assert_eq!(
            does_match("member \"Hello World\" -raw desc More text goes here", &pat),
            Some(MatchResult {
                parameters: vec![ParameterMatch {
                    name: "member_ref".to_string(),
                    value: "Hello World".to_string(),
                    span: 7..20
                }],
                flags: vec![FlagMatch {
                    name: "raw".to_string(),
                    value: None
                }],
                remainder: Some("More text goes here".to_string())
            })
        );
    }
}
