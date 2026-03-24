use std::fmt;

use pluralkit_models::{GroupId, MemberId, SwitchId, SystemId};
use sqlx::FromRow;

// don't worry about these traits requirements here. for self-contained simple structs, they're all trivially satisfied
pub trait OwningSystem: Clone + fmt::Debug + Send + Sync + 'static {
    fn system_id(&self) -> SystemId;
}

#[derive(Clone, FromRow, Debug)]
pub struct RequestAboutSystem {
    pub id: SystemId,
}

impl OwningSystem for RequestAboutSystem {
    fn system_id(&self) -> SystemId {
        self.id
    }
}

impl From<(SystemId,)> for RequestAboutSystem {
    fn from((id,): (SystemId,)) -> Self {
        RequestAboutSystem { id }
    }
}

#[derive(Clone, FromRow, Debug)]
pub struct RequestAboutMember {
    #[allow(dead_code)]
    pub id: MemberId,
    pub system: SystemId,
}

impl OwningSystem for RequestAboutMember {
    fn system_id(&self) -> SystemId {
        self.system
    }
}

impl From<(MemberId, SystemId)> for RequestAboutMember {
    fn from((id, system): (MemberId, SystemId)) -> Self {
        RequestAboutMember { id, system }
    }
}

#[derive(Clone, FromRow, Debug)]
pub struct RequestAboutGroup {
    #[allow(dead_code)]
    pub id: GroupId,
    pub system: SystemId,
}

impl OwningSystem for RequestAboutGroup {
    fn system_id(&self) -> SystemId {
        self.system
    }
}

impl From<(GroupId, SystemId)> for RequestAboutGroup {
    fn from((id, system): (GroupId, SystemId)) -> Self {
        RequestAboutGroup { id, system }
    }
}

#[derive(Clone, FromRow, Debug)]
pub struct RequestAboutSwitch {
    #[allow(dead_code)]
    pub id: SwitchId,
    pub system: SystemId,
}

impl OwningSystem for RequestAboutSwitch {
    fn system_id(&self) -> SystemId {
        self.system
    }
}

impl From<(SwitchId, SystemId)> for RequestAboutSwitch {
    fn from((id, system): (SwitchId, SystemId)) -> Self {
        RequestAboutSwitch { id, system }
    }
}
