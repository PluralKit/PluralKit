use std::collections::HashMap;

use smol_str::SmolStr;

lazy_static::lazy_static! {
    // Dictionary of (left, right) quote pairs
    // Each char in the string is an individual quote, multi-char strings imply "one of the following chars"
    // Certain languages can have quote patterns that have a different character for open and close
    pub static ref QUOTE_PAIRS: HashMap<String, String> = {
        let mut pairs = HashMap::new();

        macro_rules! insert_pair {
            ($a:literal, $b:literal) => {
                pairs.insert($a.to_string(), $b.to_string());
                // make it easier to look up right quotes
                for char in $a.chars() {
                    pairs.insert(char.to_string(), $b.to_string());
                }
            }
        }

        // Basic
        insert_pair!( "'", "'" ); // ASCII single quotes
        insert_pair!( "\"", "\"" ); // ASCII double quotes

        // "Smart quotes"
        // Specifically ignore the left/right status of the quotes and match any combination of them
        // Left string also includes "low" quotes to allow for the low-high style used in some locales
        insert_pair!( "\u{201C}\u{201D}\u{201F}\u{201E}", "\u{201C}\u{201D}\u{201F}" ); // double quotes
        insert_pair!( "\u{2018}\u{2019}\u{201B}\u{201A}", "\u{2018}\u{2019}\u{201B}" ); // single quotes

        // Chevrons (normal and "fullwidth" variants)
        insert_pair!( "\u{00AB}\u{300A}", "\u{00BB}\u{300B}" ); // double chevrons, pointing away (<<text>>)
        insert_pair!( "\u{00BB}\u{300B}", "\u{00AB}\u{300A}" ); // double chevrons, pointing together (>>text<<)
        insert_pair!( "\u{2039}\u{3008}", "\u{203A}\u{3009}" ); // single chevrons, pointing away (<text>)
        insert_pair!( "\u{203A}\u{3009}", "\u{2039}\u{3008}" ); // single chevrons, pointing together (>text<)

        // Other
        insert_pair!( "\u{300C}\u{300E}", "\u{300D}\u{300F}" ); // corner brackets (Japanese/Chinese)

        pairs
    };
}

// very very simple quote matching
// quotes need to be at start/end of words, and are ignored if a closing quote is not present
// WTB POSIX quoting: https://pubs.opengroup.org/onlinepubs/9799919799/utilities/V3_chap02.html
pub(super) fn next_param(input: SmolStr, current_pos: usize) -> Option<(SmolStr, usize)> {
    if input.len() == current_pos {
        return None;
    }

    let leading_whitespace_count =
        input[..current_pos].len() - input[..current_pos].trim_start().len();
    let substr_to_match: SmolStr = input[current_pos + leading_whitespace_count..].into();
    println!("stuff: {input} {current_pos} {leading_whitespace_count}");
    println!("to match: {substr_to_match}");

    // try matching end quote
    if let Some(right) = QUOTE_PAIRS.get(&substr_to_match[0..1]) {
        for possible_quote in right.chars() {
            for (pos, _) in substr_to_match.match_indices(possible_quote) {
                if substr_to_match.len() == pos + 1
                    || substr_to_match
                        .chars()
                        .nth(pos + 1)
                        .unwrap()
                        .is_whitespace()
                {
                    // return quoted string, without quotes
                    return Some((substr_to_match[1..pos - 1].into(), current_pos + pos + 1));
                }
            }
        }
    }

    // find next whitespace character
    for (pos, char) in substr_to_match.clone().char_indices() {
        if char.is_whitespace() {
            return Some((substr_to_match[..pos].into(), current_pos + pos + 1));
        }
    }

    // if we're here, we went to EOF and didn't match any whitespace
    // so we return the whole string
    Some((substr_to_match.clone(), current_pos + substr_to_match.len()))
}
