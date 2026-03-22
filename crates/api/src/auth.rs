use pluralkit_models::{PrivacyLevel, SystemId};

pub const INTERNAL_SYSTEMID_HEADER: &'static str = "x-pluralkit-systemid";
pub const INTERNAL_APPID_HEADER: &'static str = "x-pluralkit-appid";

#[derive(Clone)]
pub struct AuthState {
    system_id: Option<i32>,
    app_id: Option<i32>,
    internal: bool,
}

impl AuthState {
    pub fn new(system_id: Option<i32>, app_id: Option<i32>, internal: bool) -> Self {
        Self {
            system_id,
            app_id,
            internal,
        }
    }

    pub fn system_id(&self) -> Option<i32> {
        self.system_id
    }

    pub fn app_id(&self) -> Option<i32> {
        self.app_id
    }

    pub fn internal(&self) -> bool {
        self.internal
    }

    pub fn access_level_for(&self, requested_system_id: SystemId) -> PrivacyLevel {
        if self
            .system_id
            .map(|id| id == requested_system_id)
            .unwrap_or(false)
        {
            PrivacyLevel::Private
        } else {
            PrivacyLevel::Public
        }
    }
}
