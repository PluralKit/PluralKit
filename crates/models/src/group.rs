pub type GroupId = i32;

#[derive(Debug, Clone)]
pub enum GroupRef {
    Uuid(uuid::Uuid),
    Hid(String),
}
