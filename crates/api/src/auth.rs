use libpk::db::repository::premium::PremiumAllowances;
use pluralkit_models::{PKSystem, PrivacyLevel, SystemId};

pub const INTERNAL_SYSTEMID_HEADER: &'static str = "x-pluralkit-systemid";
pub const INTERNAL_APPID_HEADER: &'static str = "x-pluralkit-appid";

#[derive(Clone)]
pub struct AuthState {
    system_id: Option<i32>,
    app_id: Option<i32>,
    internal: bool,
    premium: Option<PremiumAllowances>,
}

impl AuthState {
    pub fn new(
        system_id: Option<i32>,
        app_id: Option<i32>,
        internal: bool,
        premium: Option<PremiumAllowances>,
    ) -> Self {
        Self {
            system_id,
            app_id,
            internal,
            premium,
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

    pub fn premium(&self) -> Option<&PremiumAllowances> {
        self.premium.as_ref()
    }

    pub fn is_premium(&self) -> bool {
        self.premium.as_ref().map_or(false, |p| p.is_active())
    }

    pub fn access_level_for(&self, a: &impl Authable) -> PrivacyLevel {
        if self
            .system_id
            .map(|id| id == a.authable_system_id())
            .unwrap_or(false)
        {
            PrivacyLevel::Private
        } else {
            PrivacyLevel::Public
        }
    }
}

// authable trait/impls

pub trait Authable {
    fn authable_system_id(&self) -> SystemId;
}

impl Authable for PKSystem {
    fn authable_system_id(&self) -> SystemId {
        self.id
    }
}
