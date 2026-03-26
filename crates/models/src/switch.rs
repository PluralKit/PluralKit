pub type SwitchId = i32;

#[derive(Debug, Clone)]
pub enum SwitchRef {
    Uuid(uuid::Uuid),
}
