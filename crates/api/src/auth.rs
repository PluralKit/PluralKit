pub const INTERNAL_SYSTEMID_HEADER: &'static str = "x-pluralkit-systemid";
pub const INTERNAL_APPID_HEADER: &'static str = "x-pluralkit-appid";

#[derive(Clone)]
pub struct AuthState {
    system_id: Option<i32>,
    app_id: Option<i32>,
}

impl AuthState {
    pub fn new(system_id: Option<i32>, app_id: Option<i32>) -> Self {
        Self { system_id, app_id }
    }

    pub fn system_id(&self) -> Option<i32> {
        self.system_id
    }

    pub fn app_id(&self) -> Option<i32> {
        self.app_id
    }
}
