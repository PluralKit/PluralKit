use uuid::Uuid;

use pluralkit_models::{PKSystem, PrivacyLevel, SystemId};

pub const INTERNAL_SYSTEMID_HEADER: &'static str = "x-pluralkit-systemid";
pub const INTERNAL_APPID_HEADER: &'static str = "x-pluralkit-appid";
pub const INTERNAL_TOKENID_HEADER: &'static str = "x-pluralkit-tid";
pub const INTERNAL_PRIVACYLEVEL_HEADER: &'static str = "x-pluralkit-privacylevel";

#[derive(Debug, Clone, PartialEq, PartialOrd)]
pub enum AccessLevel {
    None = 0,
    PublicRead,
    PrivateRead,
    Full,
}

impl AccessLevel {
    pub fn privacy_level(&self) -> PrivacyLevel {
        match self {
            Self::None | Self::PublicRead => PrivacyLevel::Public,
            Self::PrivateRead | Self::Full => PrivacyLevel::Private,
        }
    }
}

#[derive(Clone)]
pub struct AuthState {
    system_id: Option<i32>,
    app_id: Option<Uuid>,
	api_key_id: Option<Uuid>,
	access_level: AccessLevel,
    internal: bool,
}

impl AuthState {
    pub fn new(system_id: Option<i32>, app_id: Option<Uuid>, api_key_id: Option<Uuid>, access_level: AccessLevel, internal: bool) -> Self {
        Self {
            system_id,
            app_id,
			api_key_id,
			access_level,
            internal,
        }
    }

    pub fn system_id(&self) -> Option<i32> {
        self.system_id
    }

    pub fn app_id(&self) -> Option<Uuid> {
        self.app_id
    }

    pub fn api_key_id(&self) -> Option<Uuid> {
        self.api_key_id
    }

	pub fn access_level(&self) -> AccessLevel {
		self.access_level.clone()
	}

    pub fn internal(&self) -> bool {
        self.internal
    }

    pub fn access_level_for(&self, a: &impl Authable) -> PrivacyLevel {
        if self
            .system_id
            .map(|id| id == a.authable_system_id())
            .unwrap_or(false)
        {
            self.access_level.privacy_level()
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
