use crate::db::ProxyTagEntry;
use tracing::info;

#[derive(Debug)]
pub struct ProxyTagMatch {
    pub inner_content: String,
    pub tags: (String, String),
    pub member_id: i32,
}

pub fn match_proxy_tags(tags: &[ProxyTagEntry], content: &str) -> Option<ProxyTagMatch> {
    let content = content.trim();

    let mut sorted_entries = tags.to_vec();
    sorted_entries.sort_by_key(|x| -((x.prefix.len() + x.suffix.len()) as i32));

    for entry in sorted_entries {
        let is_tag_match = content.starts_with(&entry.prefix) && content.ends_with(&entry.suffix);
        info!(
            "prefix: {}, suffix: {}, content: {}, is_match: {}",
            entry.prefix, entry.suffix, content, is_tag_match
        );

        // todo: extract leading mentions
        // todo: allow empty matches only if we're proxying an attachment
        // todo: properly handle <>s etc, there's some regex stuff in there i don't entirely understand
        // todo: there's some weird edge cases with various unicode control characters and emoji joiners and whatever, should figure that out + unit test it
        if is_tag_match {
            let inner_content =
                &content[entry.prefix.len()..(content.len() - entry.suffix.len())].trim();
            return Some(ProxyTagMatch {
                inner_content: inner_content.to_string(),
                tags: (entry.prefix, entry.suffix),
                member_id: entry.member_id,
            });
        }
    }

    None
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn basic_match() {
        let tags = vec![
            ("[", "]", 0).into(),
            ("[[", "]]", 1).into(),
            ("P:", "", 2).into(),
            ("", "-P", 3).into(),
            ("+ ", "", 4).into(),
        ];
        assert_no_match(&tags, "hello world");
        assert_match(&tags, "[hello world]", "hello world", 0);
        assert_match(&tags, "[   hello world    ]", "hello world", 0);
        assert_match(&tags, "    [  hello world   ]   ", "hello world", 0);
        assert_match(&tags, "[\nhello\n]", "hello", 0);
        assert_match(&tags, "[\nhello\nworld\n]", "hello\nworld", 0);

        assert_match(&tags, "[[text]]", "text", 1);
        assert_match(&tags, "[text]]", "text]", 0);
        assert_match(&tags, "[[[text]]]", "[text]", 1);

        assert_match(&tags, "P:text", "text", 2);
        assert_match(&tags, "text -P", "text", 3);

        assert_match(&tags, "+ hello", "hello", 4);
        assert_no_match(&tags, "+hello"); // (prefix contains trailing space)

        assert_match(&tags, "[]", "", 0);

        // edge case: the c# implementation currently does what the commented out test does
        // *if* the message doesn't have an attachment. not sure if we should mirror this here.
        // assert_match(&tags, "[[]]", "[]", 0);
        assert_match(&tags, "[[]]", "", 1);
    }

    fn assert_match(tags: &[ProxyTagEntry], message: &str, inner: &str, member: i32) {
        let res = match_proxy_tags(tags, message);
        assert_eq!(
            res.as_ref()
                .map(|x| (x.inner_content.as_str(), x.member_id)),
            Some((inner, member))
        );
    }

    fn assert_no_match(tags: &[ProxyTagEntry], message: &str) {
        let res = match_proxy_tags(tags, message);
        assert!(res.is_none());
    }
}
