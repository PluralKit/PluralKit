# PluralKit development documentation

Most of PluralKit's code is written in C#, and is split into 3 projects: `PluralKit.Core` (supporting libraries), `PluralKit.Bot` (the Discord bot), and `PluralKit.API` (ASP.NET webserver with controllers for most API endpoints).

There is an ongoing effort to port this code to Rust, and we have a handful of crates already. 
Currently, the main Rust services are:
- `gateway` - connects to Discord to receive events
- `api` - handles authentication and rate-limiting for the public API, as well as a couple of private endpoints
- `scheduled_tasks` - background cron job runner, for statistics and miscellaneous cleanup.
- `avatars` - handles avatar storage and cleanup
- `dispatch` - dispatches webhook events

Additionally, `libpk` handles runtime configuration and database functions.

At the very least, `PluralKit.Bot` and `gateway` are required for the bot to run. While code still exists to connect to the Discord gateway directly from the C# bot, this is no longer a supported configuration and may break in the future.

Service-specific documentation can be found for the C# services in [dotnet.md](./dotnet.md), and for the Rust services in [rust.md](./rust.md).

## Building/running

PluralKit uses a PostgreSQL database and a Redis database to store data. User-provided data is stored in Postgres; Redis is used for internal state and transient data such as the command execution cache. It's generally easy to run these in Docker or with the Nix `process-compose`, but any install method should work.

The production instance of PluralKit uses Docker images built in CI. These take a long time to rebuild and aren't good for development (they're production builds, so it's not possible to hook up a debugger). Instead, it's preferable to install build dependencies locally. This is easy with the provided Nix flake: run `nix develop .#bot` to drop into a shell with all the C# build dependencies available, and `nix develop .#services` to get a shell with the Rust build dependencies. It's also okay to manually install build dependencies if you prefer.

PluralKit services are configured with environment variables; see service-specific documentation for details. Generally, the configuration from the self-host `docker-compose.yml` should get you started.

### process-compose basic steps

Your .env should contain at least the following for the bot to run (see the C#/Rust service specific docs for more on configuration):
```
pluralkit__discord__bot_token="<YOUR_BOT_TOKEN_HERE>"
PluralKit__Bot__Token="<YOUR_BOT_TOKEN_HERE>"
pluralkit__discord__client_id="<YOUR_CLIENT_ID_HERE>"
PluralKit__Bot__Client="<YOUR_CLIENT_ID_HERE>"

RUST_LOG="info"
pluralkit__db__db_password="postgres"
pluralkit__db__data_db_uri="postgresql://postgres@localhost:5432/pluralkit"
pluralkit__db__data_redis_addr="redis://localhost:6379"
pluralkit__discord__client_secret=1
pluralkit__discord__max_concurrency=1
pluralkit__discord__gateway_target="http://localhost:5002/events"
pluralkit__runtime_config_key=gateway
PluralKit__Database="Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=pluralkit"
PluralKit__RedisAddr="localhost:6379"
PluralKit__Bot__DisableGateway="true"
PluralKit__Bot__EventAwaiterTarget="http://localhost:5002/events"
PluralKit__Bot__HttpListenerAddr="127.0.0.1"
PluralKit__Bot__HttpCacheUrl="localhost:5000"
```

1. Clone the repository: `git clone --recurse-submodules https://github.com/PluralKit/PluralKit`
2. Create a `.env` configuration file in the `PluralKit` directory *(see above)*
3. Build and run: `nix run .#dev`
	- This will download the dependencies, build, and run PluralKit
	- If Nix is not setup to allow flakes, you may need to add `--extra-experimental-features nix-command --extra-experimental-features flakes` to the command
	- If the `pluralkit-bot` process fails to run, you can restart it by selecting it and pressing `Ctrl-R`
```
[nix-shell:~]$ git clone --recurse-submodules https://github.com/PluralKit/PluralKit
[nix-shell:~]$ cd PluralKit
[nix-shell:~/PluralKit]$ nano .env
[nix-shell:~/PluralKit]$ nix run .#dev
```

## Upgrading database from legacy version
If you have an instance of the Python version of the bot (from the `legacy` branch), you may need to take extra database migration steps.
For more information, see [LEGACYMIGRATE.md](./LEGACYMIGRATE.md).
