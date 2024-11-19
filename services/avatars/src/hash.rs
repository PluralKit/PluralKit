use std::fmt::Display;

use sha2::{Digest, Sha256};

#[derive(Debug)]
pub struct Hash([u8; 32]);

impl Hash {
    pub fn sha256(data: &[u8]) -> Hash {
        let mut hasher = Sha256::new();
        hasher.update(data);
        Hash(hasher.finalize().into())
    }
}

impl Display for Hash {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let encoding = data_encoding::BASE32_NOPAD;
        write!(f, "{}", encoding.encode(&self.0[..16]).to_lowercase())
    }
}
