use aws_sdk_s3::primitives::ByteStream;
use tracing::error;

use crate::process::ProcessOutput;

pub struct StoreResult {
    pub id: String,
    pub path: String,
}

pub async fn store(
    client: &aws_sdk_s3::Client,
    bucket: &str,
    res: &ProcessOutput,
) -> anyhow::Result<StoreResult> {
    // errors here are all going to be internal
    let encoded_hash = res.hash.to_string();
    let path = format!(
        "images/{}/{}.{}",
        &encoded_hash[..2],
        &encoded_hash[2..],
        res.format.extension()
    );

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
            .content_type(res.format.mime_type())
            .send()
            .await
        {
            Ok(_) => {
                tracing::debug!("uploaded image to {}", &path);
                return Ok(StoreResult {
                    id: encoded_hash,
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
