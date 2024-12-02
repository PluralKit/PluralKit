use lapin::options::BasicAckOptions;
use tokio_stream::StreamExt;
use tracing::info;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    libpk::init_logging("api")?;
    libpk::init_metrics()?;
    info!("hello world");

    let mut rabbit_client = libpk::rabbit::new().await?;

    rabbit_client.setup_queue("pk-commands").await?;

    while let Some(delivery) = rabbit_client.consume("pk-commands").await?.next().await {
        let delivery = match delivery {
            Ok(delivery) => delivery,
            Err(error) => {
                dbg!("Failed to consume queue message {}", error);
                continue;
            }
        };

        // Do something with the delivery data (The message payload)
        info!(
            "got an event {:#?}",
            String::from_utf8(delivery.data.clone()).unwrap()
        );

        delivery
            .ack(BasicAckOptions::default())
            .await
            .expect("Failed to ack send_webhook_event message");
    }

    std::future::pending::<()>().await;

    Ok(())
}
