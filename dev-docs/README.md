# PluralKit development documentation

Most of PluralKit's code is written in C#, and is split into 3 projects: `PluralKit.Core` (supporting libraries), `PluralKit.Bot` (the Discord bot), and `PluralKit.API` (ASP.NET webserver with controllers for most API endpoints).

There is an ongoing effort to port this code to Rust, and we have a handful of crates already. Currently, the main Rust services are `gateway` (connects to Discord to receive events), `api` (handles authentication and rate-limiting for the public API, as well as a couple of private endpoints), and `scheduled_tasks` (background cron job runner, for statistics and miscellaneous cleanup).

At the very least, `PluralKit.Bot` and `gateway` are required for the bot to run. While code still exists to connect to the Discord gateway directly from the C# bot, this is no longer a supported configuration and may break in the future.

Service-specific documentation can be found for the C# services in [dotnet.md](./dotnet.md), and for the Rust services in [rust.md](./rust.md) (todo; there is a temporary mostly-accurate document at [RUNNING.md](./RUNNING.md)).

## Building/running

PluralKit uses a PostgreSQL database and a Redis database to store data. User-provided data is stored in Postgres; Redis is used for internal state and transient data such as the command execution cache. It's generally easy to run these in Docker or with the Nix `process-compose`, but any install method should work.

The production instance of PluralKit uses Docker images built in CI. These take a long time to rebuild and aren't good for development (they're production builds, so it's not possible to hook up a debugger). Instead, it's preferable to install build dependencies locally. This is easy with the provided Nix flake: run `nix develop .#bot` to drop into a shell with all the C# build dependencies available, and `nix develop .#services` to get a shell with the Rust build dependencies. It's also okay to manually install build dependencies if you prefer.

PluralKit services are configured with environment variables; see service-specific documentation for details. Generally, the configuration from the self-host `docker-compose.yml` should get you started.

## Upgrading database from legacy version
If you have an instance of the Python version of the bot (from the `legacy` branch), you may need to take extra database migration steps.
For more information, see [LEGACYMIGRATE.md](./LEGACYMIGRATE.md).
