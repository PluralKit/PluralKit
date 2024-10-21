use crate::process::ProcessOutput;
use tracing::error;

pub struct StoreResult {
    pub id: String,
    pub path: String,
}

pub async fn store(bucket: &s3::Bucket, res: &ProcessOutput) -> anyhow::Result<StoreResult> {
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
            anyhow::bail!("error uploading image to cdn, too many retries") // nicer user-facing error?
        }
        retry_count += 1;

        let resp = bucket
            .put_object_with_content_type(&path, &res.data, res.format.mime_type())
            .await?;
        match resp.status_code() {
            200 => {
                tracing::debug!("uploaded image to {}", &path);

                return Ok(StoreResult {
                    id: encoded_hash,
                    path,
                });
            }
            500 | 503 => {
                tracing::warn!(
                    "got 503 uploading image to {} ({}), retrying... (try {}/3)",
                    &path,
                    resp.as_str()?,
                    retry_count
                );
                continue;
            }
            _ => {
                error!(
                    "storage backend responded status code {}",
                    resp.status_code()
                );
                anyhow::bail!("error uploading image to cdn") // nicer user-facing error?
            }
        }
    }
}
