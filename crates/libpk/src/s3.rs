use aws_sdk_s3::config::{Credentials, Region};

use crate::_config::S3Config;

pub fn create_client(config: &S3Config) -> aws_sdk_s3::Client {
    let creds = Credentials::new(
        &config.application_id,
        &config.application_key,
        None,
        None,
        "static",
    );
    let s3_config = aws_sdk_s3::config::Builder::new()
        .endpoint_url(&config.endpoint)
        .region(Region::new("auto"))
        .credentials_provider(creds)
        .build();
    aws_sdk_s3::Client::from_conf(s3_config)
}
