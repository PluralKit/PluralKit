use std::ops::Range;

#[derive(Debug, Clone, PartialEq)]
pub enum Token {
    Keyword {
        value: String,
        quoted: bool,
        span: Range<usize>,
    },
    Flag {
        name: String,
        value: Option<String>,
        span: Range<usize>,
    },
}

pub struct Tokenizer<'a> {
    inner: &'a str,
    words: WordSpanIterator<'a>,
}

impl<'a> Tokenizer<'a> {
    pub fn new(s: &str) -> Tokenizer {
        Tokenizer {
            inner: s,
            words: WordSpanIterator::new(s),
        }
    }

    fn next_token(&mut self) -> Option<Token> {
        self.words.next().map(|span| {
            let word = &self.inner[span.clone()];

            if let Some((inner, quoted_span)) = self.try_read_quoted_token(span.clone()) {
                Token::Keyword {
                    value: inner.to_string(),
                    quoted: true,
                    span: quoted_span,
                }
            } else if word.starts_with("-") {
                let flag_name = word.trim_start_matches('-');

                let (flag_name, flag_value) = match flag_name.split_once('=') {
                    Some((flag_name, flag_value)) => {
                        (flag_name.to_string(), Some(flag_value.to_string()))
                    }
                    None => (flag_name.to_string(), None),
                };

                Token::Flag {
                    name: flag_name,
                    value: flag_value,
                    span,
                }
            } else {
                Token::Keyword {
                    value: word.to_string(),
                    quoted: false,
                    span: span,
                }
            }
        })
    }

    fn try_read_quoted_token(&mut self, span: Range<usize>) -> Option<(&str, Range<usize>)> {
        let start_pos = span.start;
        find_quote_pair(&self.inner[span.start..]).map(|(left_quote, right_quotes)| {
            let mut word_span = span;
            word_span.start += left_quote.len_utf8();

            // effectively do-while-let but rust doesn't have that :/
            loop {
                let end_word = &self.inner[word_span.clone()];

                for right_quote in right_quotes {
                    if end_word.ends_with(*right_quote) {
                        let end_pos = word_span.end;
                        let inner_span =
                            (start_pos + left_quote.len_utf8())..(end_pos - right_quote.len_utf8());
                        let inner_str = &self.inner[inner_span];
                        return (inner_str, start_pos..end_pos);
                    }
                }

                if let Some(next_word_span) = self.words.next() {
                    word_span = next_word_span;
                } else {
                    break;
                }
            }

            (&self.inner[start_pos..], start_pos..self.inner.len())
        })
    }
}

impl<'a> Iterator for Tokenizer<'a> {
    type Item = Token;

    fn next(&mut self) -> Option<Self::Item> {
        self.next_token()
    }
}

struct WordSpanIterator<'a> {
    iter: std::str::SplitInclusive<'a, fn(char) -> bool>,
    pos: usize,
}

impl<'a> WordSpanIterator<'a> {
    fn new(s: &'a str) -> WordSpanIterator<'a> {
        WordSpanIterator {
            iter: s.split_inclusive(char::is_whitespace),
            pos: 0,
        }
    }
}

impl<'a> Iterator for WordSpanIterator<'a> {
    type Item = Range<usize>;

    fn next(&mut self) -> Option<Self::Item> {
        while let Some(word) = self.iter.next() {
            let word_start = self.pos;
            self.pos += word.len();

            let trimmed = word.trim_end();
            if word.trim_end().len() > 0 {
                let trimmed_span = word_start..(word_start + trimmed.len());
                return Some(trimmed_span);
            }
        }

        None
    }
}

fn find_quote_pair(s: &str) -> Option<(char, &'static [char])> {
    s.chars()
        .next()
        .and_then(|c| matching_quotes(c).map(|x| (c, x)))
}

fn matching_quotes(c: char) -> Option<&'static [char]> {
    match c {
        // Basic
        '"' => Some(&['"']),
        '\'' => Some(&['\'']),

        // "Smart quotes"
        // Specifically ignore the left/right status of the quotes and match any combination of them
        // Left string also includes "low" quotes to allow for the low-high style used in some locales
        '\u{201c}' | '\u{201d}' | '\u{201f}' | '\u{201e}' => {
            Some(&['\u{201c}', '\u{201d}', '\u{201f}'])
        } // double
        '\u{2018}' | '\u{2019}' | '\u{201b}' | '\u{201a}' => {
            Some(&['\u{2018}', '\u{2019}', '\u{201b}'])
        } // single

        // Chevrons (normal and "fullwidth" variants)
        '\u{00ab}' | '\u{300a}' => Some(&['\u{00bb}', '\u{300b}']), // double chevrons, pointing away (<<text>>)
        '\u{00bb}' | '\u{300b}' => Some(&['\u{00aa}', '\u{300a}']), // double chevrons, pointing together (>>text<<)
        '\u{2039}' | '\u{3008}' => Some(&['\u{203a}', '\u{3009}']), // single chevrons, pointing away (<text>)
        '\u{203a}' | '\u{3009}' => Some(&['\u{2039}', '\u{3008}']), // single chevrons, pointing together (>text<)

        // Other
        '\u{300c}' | '\u{300e}' => Some(&['\u{300d}', '\u{300f}']), // corner brackets (Japanese/Chinese)

        _ => None,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn basic_words() {
        let s = "hello world abcdefg";
        let mut tk = Tokenizer::new(s);

        assert_word(tk.next(), "hello", false, 0..5);
        assert_word(tk.next(), "world", false, 6..11);
        assert_word(tk.next(), "abcdefg", false, 12..19);
    }

    #[test]
    fn ignore_whitespace() {
        // U+2003 EM SPACE is 3 utf-8 bytes
        let s = "    lotsa   \u{2003}    spaces \t  and \t\n\t stuff     \n";
        let mut tk = Tokenizer::new(s);

        assert_word(tk.next(), "lotsa", false, 4..9);
        assert_word(tk.next(), "spaces", false, 19..25);
        assert_word(tk.next(), "and", false, 29..32);
        assert_word(tk.next(), "stuff", false, 37..42);
    }

    #[test]
    fn quoted_words() {
        let mut tk = Tokenizer::new("hello \"in double quotes\" 'and single quotes'");
        assert_word(tk.next(), "hello", false, 0..5);
        assert_word(tk.next(), "in double quotes", true, 6..24);
        assert_word(tk.next(), "and single quotes", true, 25..44);

        let mut tk = Tokenizer::new("\"quote at start of\" string");
        assert_word(tk.next(), "quote at start of", true, 0..19);
        assert_word(tk.next(), "string", false, 20..26);

        let mut tk = Tokenizer::new("\"\n  include whitespace\nin quotes\n\"");
        assert_word(
            tk.next(),
            "\n  include whitespace\nin quotes\n",
            true,
            0..34,
        );

        let mut tk = Tokenizer::new("'it's 5 o'clock' said o'brian");
        assert_word(tk.next(), "it's 5 o'clock", true, 0..16);
        assert_word(tk.next(), "said", false, 17..21);
        assert_word(tk.next(), "o'brian", false, 22..29);
    }

    #[test]
    fn flags() {
        let mut tk = Tokenizer::new("word -flag and-word");
        assert_word(tk.next(), "word", false, 0..4);
        assert_flag(tk.next(), "flag", None, 5..10);
        assert_word(tk.next(), "and-word", false, 11..19);

        let mut tk = Tokenizer::new("-lots --of ---dashes");
        assert_flag(tk.next(), "lots", None, 0..5);
        assert_flag(tk.next(), "of", None, 6..10);
        assert_flag(tk.next(), "dashes", None, 11..20);
    }

    #[test]
    fn flag_values() {
        let mut tk = Tokenizer::new("-flag=value --flag2=value2 -flag3=value3=more");
        assert_flag(tk.next(), "flag", Some("value"), 0..11);
        assert_flag(tk.next(), "flag2", Some("value2"), 12..26);
        assert_flag(tk.next(), "flag3", Some("value3=more"), 27..45);
    }

    fn assert_word(tk: Option<Token>, s: &str, quoted: bool, span: Range<usize>) {
        assert_eq!(
            tk,
            Some(Token::Keyword {
                value: s.to_string(),
                quoted: quoted,
                span: span
            })
        );
    }

    fn assert_flag(tk: Option<Token>, name: &str, value: Option<&str>, span: Range<usize>) {
        assert_eq!(
            tk,
            Some(Token::Flag {
                name: name.to_string(),
                span: span,
                value: value.map(|x| x.to_string())
            })
        );
    }
}
