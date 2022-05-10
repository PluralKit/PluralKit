use deadpool_postgres::Pool;
use redis::AsyncCommands;
use twilight_model::gateway::event::GatewayEventDeserializer;
use std::sync::Arc;
use tracing::info;

use twilight_gateway::Event;
use twilight_http::Client as HttpClient;

lazy_static::lazy_static! {
    static ref ALLOWED_EVENTS: Vec<&'static str> = [
        "INTERACTION_CREATE",
        "MESSAGE_CREATE",
        "MESSAGE_DELETE",
        "MESSAGE_DELETE_BULK",
        "MESSAGE_UPDATE",
        "MESSAGE_REACTION_ADD",
    ].to_vec();
}

pub async fn handle_event<'a>(
    shard_id: u64,
    event: Event,
    http: Arc<HttpClient>,
    _db: Pool,
    rconn: redis::Client
) -> anyhow::Result<()> {
    myriad::cache::handle_event(event.clone(), rconn.clone()).await?;

    match event {
        Event::GatewayInvalidateSession(resumable) => {
            info!("shard {} session invalidated, resumable? {}", shard_id, resumable);
        }
        Event::ShardConnected(_) => {
            info!("shard {} connected", shard_id);
        }
        Event::ShardDisconnected(info) => {
            info!("shard {} disconnected, code: {:?}, reason: {:?}", shard_id, info.code, info.reason);
        }
        Event::ShardPayload(payload) => {
            let deserializer = GatewayEventDeserializer::from_json(std::str::from_utf8(&payload.bytes)?).unwrap();
            if deserializer.op() == 0 && ALLOWED_EVENTS.contains(&deserializer.event_type_ref().unwrap()) {
                let mut conn = rconn.get_async_connection().await?;
                conn.publish::<&str, Vec<u8>, i32>(&format!("evt-{shard_id}"), payload.bytes).await?;
            }
        }
        Event::MessageCreate(msg) => {
            if msg.content == "pkt;test" {
                // let message_context = db::get_message_context(
                //     &db,
                //     msg.author.id.get(),
                //     msg.guild_id.map(|x| x.get()).unwrap_or(0),
                //     msg.channel_id.get(),
                // )
                // .await?;

                // let content = format!("message context:\n```\n{:#?}\n```", message_context);
                // http.create_message(msg.channel_id)
                //     .reply(msg.id)
                //     .content(&content)?
                //     .exec()
                //     .await?;

                // let proxy_members = db::get_proxy_members(
                //     &db,
                //     msg.author.id.get(),
                //     msg.guild_id.map(|x| x.get()).unwrap_or(0),
                // )
                // .await?;

                // let content = format!("proxy members:\n```\n{:#?}\n```", proxy_members);
                // info!("{}", content);
                // http.create_message(msg.channel_id)
                //     .reply(msg.id)
                //     .content(&content)?
                //     .exec()
                //     .await?;

                // let cache_stats = cache.stats();

                // let pid = unsafe { libc::getpid() };
                // let pagesize = {
                //     unsafe {
                //         libc::sysconf(libc::_SC_PAGESIZE)
                //     }
                // };
                
                // let p = procfs::process::Process::new(pid)?;
                // let content = format!(
                //     "[rust]\nguilds:{}\nchannels:{}\nroles:{}\nusers:{}\nmembers:{}\n\nmemory usage: {}",
                //     cache_stats.guilds(),
                //     cache_stats.channels(),
                //     cache_stats.roles(),
                //     cache_stats.users(),
                //     cache_stats.members(),
                //     p.stat.rss * pagesize
                // );

                // http.create_message(msg.channel_id)
                // .reply(msg.id)
                // .content(&content)?
                // .exec()
                // .await?;
            }
        }
        _ => {}
    }

    Ok(())
}
