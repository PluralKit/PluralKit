use std::io::Result;

fn main() -> Result<()> {
    prost_build::compile_protos(&["../proto/discord_cache.proto"], &["../proto/"])?;
    Ok(())
}