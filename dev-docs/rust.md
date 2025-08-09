## Services Overview
TODO: write more about what each service does (and quirks)

## Configuration
Configuration is done through environment variables. A potentially uncompleted and/or outdated list is as follows:

#### Key:
- G - gateway
- A - api
- ST - scheduled_tasks
- AV - avatars
- D - dispatch

| Used by:        | Name                                                     | Description                                                                                                                                         |
| --------------- | -------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| G, ST           | **`pluralkit__discord__bot_token`**                      | the Discord bot token to connect with                                                                                                               |
| G, A            | **`pluralkit__discord__client_id`**                      | the ID of the bot's user account, used for calculating the bot's own permissions and for the link in `pk;invite`.                                   |
| A               | **`pluralkit__discord__client_secret`**                  | the client secret of the application, used for OAuth with Discord                                                                                   |
| G               | **`pluralkit__discord__cluster__total_shards`**          | the total number of shards                                                                                                                          |
| G               | **`pluralkit__discord__cluster__total_nodes`**           | the total number of clusters                                                                                                                        |
| G               | **`pluralkit__discord__cluster__node_id`**               | the ID of the cluster (overwritten at runtime when operating under managers that can't template the node id into this variable, such as kubernetes) |
| G               | **`pluralkit__discord__max_concurrency`**                | number of identify requests per 5 seconds -- see Discord docs                                                                                       |
| G               | **`pluralkit__discord__gateway_target`**                 | the URL of a dotnet bot instance to send events to                                                                                                  |
| G               | **`pluralkit__discord__bot_prefix_for_gateway`**         | the prefix to show in the bot's activity status. If not specified will use `pk;`                                                                    |
| G, ST           | **`pluralkit__discord__api_base_url`**                   | the base Discord API url used for HTTP API requests                                                                                                 |
| G, A, ST, AV    | **`pluralkit__db__data_db_uri`**                         | the URI of the PostgreSQL data database in [libpq format](https://www.postgresql.org/docs/current/libpq-connect.html#LIBPQ-CONNSTRING)              |
| G, A, ST, AV    | **`pluralkit__db__data_redis_addr`**                     | the address of the Redis instance, in [standard Redis format](https://redis.io/docs/latest/develop/clients/nodejs/connect/)                         |
| G, A, ST, AV    | **`pluralkit__db__db_password`**                         | the password to use for PostgreSQL database connections                                                                                             |
| G, A, ST, AV    | **`pluralkit__db__messages_db_uri`**                     | the URI of the PostgreSQL messages database in [libpq format](https://www.postgresql.org/docs/current/libpq-connect.html#LIBPQ-CONNSTRING)          |
| G, A, ST, AV    | **`pluralkit__db__stats_db_uri`**                        | the URI of the PostgreSQL statistics database in [libpq format](https://www.postgresql.org/docs/current/libpq-connect.html#LIBPQ-CONNSTRING)        |
| ST              | **`pluralkit__scheduled_tasks__expected_gateway_count`** | the total number of expected running gateway instances                                                                                              |
| ST              | **`pluralkit__scheduled_tasks__gateway_url`**            | the base URL used for querying statistics from gateway instances                                                                                    |
| ST              | **`pluralkit__scheduled_tasks__set_guild_count`**        | boolean used to determine if the guild count should be updated in Redis for the bot status                                                          |
| A               | **`pluralkit__api__addr`**                               | the bind address used for the Rust API                                                                                                              |
| A               | **`pluralkit__api__ratelimit_redis_addr`**               | the address of a Redis instance to use for request ratelimiting                                                                                     |
| A               | **`pluralkit__api__remote_url`**                         | the remote url of the dotnet API instance                                                                                                           |
| A               | **`pluralkit__api__temp_token2`**                        | the token used in the API for fetching app authorization                                                                                            |
| AV              | **`pluralkit__avatars__cdn_url`**                        | the CDN address used for avatar storage                                                                                                             |
| AV              | **`pluralkit__avatars__cloudflare_token`**               | the Cloudflare token to use for avatar cache cleanup                                                                                                |
| AV              | **`pluralkit__avatars__cloudflare_zone_id`**             | the Cloudflare zone id to use for avatar cache cleanup                                                                                              |
| AV              | **`pluralkit__avatars__s3__application_id`**             | the application id of the s3 instance to use for avatar storage                                                                                     |
| AV              | **`pluralkit__avatars__s3__application_key`**            | the application key of the s3 instance to use for avatar storage                                                                                    |
| AV              | **`pluralkit__avatars__s3__bucket`**                     | the bucket to use for avatar storage                                                                                                                |
| AV              | **`pluralkit__avatars__s3__endpoint`**                   | the endpoint URL of the s3 instance to use for avatar storage                                                                                       |
| G, A, ST, AV, D | **`pluralkit__json_log`**                                | boolean used to enable or disable JSON log formatting                                                                                               |
| G               | **`pluralkit__runtime_config_key`**                      | the instance identifier key used when fetching configuration from Redis at runtime to differentiate gateway instances (ex. 'gateway')               |
| G, A, ST, AV, D | **`pluralkit__run_metrics_server`**                      | boolean used to enable or disable the inbuilt Prometheus format metrics server                                                                      |
| G, A, ST, AV, D | **`pluralkit__sentry_url`**                              | the URL of a sentry instance to publish errors to                                                                                                   |
