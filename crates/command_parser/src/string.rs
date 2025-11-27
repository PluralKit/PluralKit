use std::collections::HashMap;

use smol_str::{SmolStr, ToSmolStr};

lazy_static::lazy_static! {
    // Dictionary of (left, right) quote pairs
    // Each char in the string is an individual quote, multi-char strings imply "one of the following chars"
    // Certain languages can have quote patterns that have a different character for open and close
    pub static ref QUOTE_PAIRS: HashMap<SmolStr, SmolStr> = {
        let mut pairs: HashMap<SmolStr, SmolStr> = HashMap::new();

        let mut insert_pair = |a: &'static str, b: &'static str| {
            let a = SmolStr::new_static(a);
            let b = SmolStr::new_static(b);
            pairs.insert(a.clone(), b.clone());
            // make it easier to look up right quotes
            for char in a.chars() {
                pairs.insert(char.to_smolstr(), b.clone());
            }
        };

        // Basic
        insert_pair( "'", "'" ); // ASCII single quotes
        insert_pair( "\"", "\"" ); // ASCII double quotes

        // "Smart quotes"
        // Specifically ignore the left/right status of the quotes and match any combination of them
        // Left string also includes "low" quotes to allow for the low-high style used in some locales
        insert_pair( "\u{201C}\u{201D}\u{201F}\u{201E}", "\u{201C}\u{201D}\u{201F}" ); // double quotes
        insert_pair( "\u{2018}\u{2019}\u{201B}\u{201A}", "\u{2018}\u{2019}\u{201B}" ); // single quotes

        // Chevrons (normal and "fullwidth" variants)
        insert_pair( "\u{00AB}\u{300A}", "\u{00BB}\u{300B}" ); // double chevrons, pointing away (<<text>>)
        insert_pair( "\u{00BB}\u{300B}", "\u{00AB}\u{300A}" ); // double chevrons, pointing together (>>text<<)
        insert_pair( "\u{2039}\u{3008}", "\u{203A}\u{3009}" ); // single chevrons, pointing away (<text>)
        insert_pair( "\u{203A}\u{3009}", "\u{2039}\u{3008}" ); // single chevrons, pointing together (>text<)

        // Other
        insert_pair( "\u{300C}\u{300E}", "\u{300D}\u{300F}" ); // corner brackets (Japanese/Chinese)

        pairs
    };
}

// very very simple quote matching
// expects match_str to be trimmed (no whitespace, from the start at least)
// returns the position of an end quote if any is found
// quotes need to be at start/end of words, and are ignored if a closing quote is not present
// WTB POSIX quoting: https://pubs.opengroup.org/onlinepubs/9799919799/utilities/V3_chap02.html
fn find_quotes(match_str: &str) -> Option<usize> {
    if let Some(right) = QUOTE_PAIRS.get(&match_str[0..match_str.ceil_char_boundary(1)]) {
        // try matching end quote
        for possible_quote in right.chars() {
            for (pos, _) in match_str.match_indices(possible_quote) {
                if match_str.len() == pos + 1
                    || match_str.chars().nth(pos + 1).unwrap().is_whitespace()
                {
                    return Some(pos);
                }
            }
        }
    }
    None
}

#[derive(Debug)]
pub(super) struct MatchedParam<'a> {
    pub(super) value: &'a str,
    pub(super) next_pos: usize,
    #[allow(dead_code)] // this'll prolly be useful sometime later
    pub(super) in_quotes: bool,
}

pub(super) fn next_param<'a>(input: &'a str, current_pos: usize) -> Option<MatchedParam<'a>> {
    if input.len() == current_pos {
        return None;
    }

    let leading_whitespace_count =
        input[..current_pos].len() - input[..current_pos].trim_start().len();
    let substr_to_match = &input[current_pos + leading_whitespace_count..];
    println!("stuff: {input} {current_pos} {leading_whitespace_count}");
    println!("to match: {substr_to_match}");

    if let Some(end_quote_pos) = find_quotes(substr_to_match) {
        // return quoted string, without quotes
        return Some(MatchedParam {
            value: &substr_to_match[1..end_quote_pos],
            next_pos: current_pos + end_quote_pos + 1,
            in_quotes: true,
        });
    }

    // find next whitespace character
    for (pos, char) in substr_to_match.char_indices() {
        if char.is_whitespace() {
            return Some(MatchedParam {
                value: &substr_to_match[..pos],
                next_pos: current_pos + pos + 1,
                in_quotes: false,
            });
        }
    }

    // if we're here, we went to EOF and didn't match any whitespace
    // so we return the whole string
    Some(MatchedParam {
        value: substr_to_match,
        next_pos: current_pos + substr_to_match.len(),
        in_quotes: false,
    })
}

#[derive(Debug, Clone)]
pub(super) struct MatchedFlag<'a> {
    pub(super) name: &'a str,
    pub(super) value: Option<&'a str>,
    pub(super) next_pos: usize,
}

pub(super) fn next_flag<'a>(input: &'a str, mut current_pos: usize) -> Option<MatchedFlag<'a>> {
    if input.len() == current_pos {
        return None;
    }

    let leading_whitespace_count =
        input[..current_pos].len() - input[..current_pos].trim_start().len();
    let substr_to_match = &input[current_pos + leading_whitespace_count..];

    // if the param is quoted, it should not be processed as a flag
    if find_quotes(substr_to_match).is_some() {
        return None;
    }

    println!("flag input {substr_to_match}");
    // strip the -
    let Some(substr_to_match) = substr_to_match.strip_prefix('-') else {
        // if it doesn't have one, then it is not a flag
        return None;
    };
    current_pos += 1;

    // try finding = or whitespace
    for (pos, char) in substr_to_match.char_indices() {
        println!("flag find char {char} at {pos}");
        if char == '=' {
            let name = &substr_to_match[..pos];
            println!("flag find {name}");
            // try to get the value
            let Some(param) = next_param(input, current_pos + pos + 1) else {
                return Some(MatchedFlag {
                    name,
                    value: Some(""),
                    next_pos: current_pos + pos + 1,
                });
            };
            return Some(MatchedFlag {
                name,
                value: Some(param.value),
                next_pos: param.next_pos,
            });
        } else if char.is_whitespace() {
            // no value if whitespace
            return Some(MatchedFlag {
                name: &substr_to_match[..pos],
                value: None,
                next_pos: current_pos + pos + 1,
            });
        }
    }

    // if eof then no value
    Some(MatchedFlag {
        name: substr_to_match,
        value: None,
        next_pos: current_pos + substr_to_match.len(),
    })
}
