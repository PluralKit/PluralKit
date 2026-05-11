use aws_sdk_s3::primitives::ByteStream;
use tracing::error;

use crate::process::ProcessOutput;

pub struct StoreResult {
    pub key: String,
    pub path: String,
}

// store image into a temporary uploads path
// api will MoveObject later to the correct storage path
pub async fn store(
    client: &aws_sdk_s3::Client,
    bucket: &str,
    uuid_key: &str,
    res: &ProcessOutput,
) -> anyhow::Result<StoreResult> {
    let ext = res
        .format
        .extensions_str()
        .first()
        .expect("expected valid extension");
    let path = format!("uploads/{}.{}", uuid_key, ext);

    // todo: something better than these retries
    let mut retry_count = 0;
    loop {
        if retry_count == 2 {
            tokio::time::sleep(tokio::time::Duration::new(2, 0)).await;
        }
        if retry_count > 2 {
            anyhow::bail!("error uploading image to cdn, too many retries")
        }
        retry_count += 1;

        match client
            .put_object()
            .bucket(bucket)
            .key(&path)
            .body(ByteStream::from(res.data.clone()))
            .content_type(res.format.to_mime_type())
            .send()
            .await
        {
            Ok(_) => {
                tracing::debug!("uploaded image to {}", &path);
                return Ok(StoreResult {
                    key: path.clone(),
                    path,
                });
            }
            Err(e) => {
                error!("error uploading image to {}: {}", &path, e);
                if retry_count > 2 {
                    anyhow::bail!(e);
                }
                tracing::warn!("retrying upload to {} (try {}/3)", &path, retry_count);
                continue;
            }
        }
    }
}
