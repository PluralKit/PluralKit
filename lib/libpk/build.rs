use std::io::Result;

fn main() -> Result<()> {
    prost_build::Config::new()
        .type_attribute(".ShardState", "#[derive(serde::Serialize)]")
        .field_attribute(".ShardState.shard_id", "#[serde(rename = \"id\")]")
        .compile_protos(&["../../proto/state.proto"], &["../../proto/"])?;
    Ok(())
}
