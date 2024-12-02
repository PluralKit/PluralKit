use lapin::{
    options::{BasicConsumeOptions, QueueDeclareOptions},
    types::{AMQPValue, FieldTable},
    Channel, Connection, ConnectionProperties, Consumer,
};
use std::sync::Arc;

pub struct PKRabbitClient {
    conn: Arc<Connection>,
    chan: Arc<Channel>,
}

fn queue_args(dlx: Option<String>) -> FieldTable {
    let mut ft = FieldTable::from(std::collections::BTreeMap::new());
    if let Some(dlx) = dlx {
        // ttl is set at message level to forward expired messages into dlx
        ft.insert(
            "x-overflow".into(),
            AMQPValue::LongString("reject-publish-dlx".into()),
        );
        ft.insert(
            "x-dead-letter-exchange".into(),
            AMQPValue::LongString("".into()),
        );
        ft.insert(
            "x-dead-letter-routing-key".into(),
            AMQPValue::LongString(dlx.into()),
        );
    } else {
        // todo: this doesn't work?
        ft.insert("x-message-ttl".into(), AMQPValue::LongInt(2000));
    }
    ft
}

pub async fn new() -> anyhow::Result<PKRabbitClient> {
    let uri = crate::config.rabbit_addr.as_ref().unwrap();
    tracing::info!("uri: {uri}");
    let options = ConnectionProperties::default()
        .with_executor(tokio_executor_trait::Tokio::current())
        .with_reactor(tokio_reactor_trait::Tokio);

    let conn = Connection::connect(uri, options).await?;
    let chan = conn.create_channel().await?;

    Ok(PKRabbitClient {
        conn: Arc::new(conn),
        chan: Arc::new(chan),
    })
}

impl PKRabbitClient {
    pub fn lapin_channel(&mut self) -> Arc<Channel> {
        self.chan.clone()
    }

    pub async fn setup_queue(&mut self, queue_name: &str) -> anyhow::Result<()> {
        let dlx_name = format!("{queue_name}-dlx");
        // declare dlx first
        self.chan
            .queue_declare(
                dlx_name.as_str(),
                QueueDeclareOptions::default(),
                queue_args(None),
            )
            .await?;

        self.chan
            .queue_declare(
                queue_name,
                QueueDeclareOptions::default(),
                queue_args(Some(dlx_name.to_string())),
            )
            .await?;

        Ok(())
    }

    pub async fn consume(&mut self, queue_name: &str) -> anyhow::Result<Consumer> {
        Ok(self
            .chan
            .basic_consume(
                queue_name,
                "",
                BasicConsumeOptions::default(),
                FieldTable::default(),
            )
            .await?)
    }

    pub async fn consume_dlx_for(&mut self, queue_name: &str) -> anyhow::Result<Consumer> {
        self.chan
            .queue_declare(
                format!("{queue_name}-dlx").as_str(),
                QueueDeclareOptions::default(),
                queue_args(None),
            )
            .await?;

        Ok(self
            .chan
            .basic_consume(
                queue_name,
                "",
                BasicConsumeOptions::default(),
                FieldTable::default(),
            )
            .await?)
    }
}
