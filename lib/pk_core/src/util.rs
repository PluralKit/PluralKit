/// get_env gets an env variable as Option<String instead of Result<String>.
pub fn get_env(key: &str) -> Option<String> {
    match std::env::var(key) {
        Ok(val) => { Some(val) }
        Err(_) => None
    }
}