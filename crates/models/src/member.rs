
pub type MemberId = i32;

#[derive(Debug, Clone)]
pub enum MemberRef {
    Uuid(uuid::Uuid),
    Hid(String),
}
