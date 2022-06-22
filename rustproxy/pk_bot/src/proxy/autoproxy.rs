use crate::db::{AutoproxyMode, AutoproxyState, MessageContext};

pub fn resolve_autoproxy_member(
    ctx: &MessageContext,
    state: &AutoproxyState,
    content: &str,
) -> Option<i32> {
    if !ctx.allow_autoproxy.unwrap_or(true) {
        return None;
    }

    if is_escape(content) {
        return None;
    }

    let first_fronter = ctx.last_switch_members.iter().flatten().cloned().next();
    match (state.autoproxy_mode, state.autoproxy_member, first_fronter) {
        (AutoproxyMode::Latch, Some(m), _) => Some(m),
        (AutoproxyMode::Member, Some(m), _) => Some(m),
        (AutoproxyMode::Front, _, Some(f)) => Some(f),
        _ => None,
    }
}

fn _is_unlatch(content: &str) -> bool {
    content.starts_with("\\\\") || content.starts_with("\\\u{200b}\\")
}

fn is_escape(content: &str) -> bool {
    content.starts_with('\\')
}
