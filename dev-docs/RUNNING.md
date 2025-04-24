# Technical Overview:

PluralKit is composed of several different parts, some of which optional and not needed for the bot to function in a testing environment.
##### Required:
- Bot (*PluralKit.Bot*)
- Gateway
- PostgreSQL Database
- Redis Database
##### Optional:
- API (*PluralKit.API*)
- Scheduled Tasks (*scheduled_tasks*)

*Optionally, it can also integrate with Sentry for error reporting, and InfluxDB for aggregate statistics. In production, we use [VictoriaMetrics](https://victoriametrics.com/) InfluxDB competible endpoint, to query metrics in a Prometheus-compatible format.

The bot and API are built using .NET 8, with other optional components used for scaling (ex. scheduled_tasks) built using Rust.
# Building + Running

**The below procedures are intended for development and testing use only!

Newer versions of the bot have moved to using [Nix](https://nixos.org/) for simplified builds. A docker compose file is also provided, but not recommended as it is not actively maintained.

See [Configuration](./CONFIGURATION.md) for full details on configuration as only the basics will be covered here.
Configuration is done through a JSON configuration file `pluralkit.conf` placed in the bot's working directory. An example of the configuration format can be seen in [pluralkit.conf.example](pluralkit.conf.example).
The minimum configuration needed for the bot to function must include the following:
- **`PluralKit.Bot.Token`**: the Discord bot token to connect with
- **`PluralKit.Bot.ClientId`**: the ID of the bot's user account, used for calculating the bot's own permissions and for the link in `pk;invite`
- **`PluralKit.Database`**: the URI of the PostgreSQL database to connect to (in [ADO.NET Npgsql format](https://www.connectionstrings.com/npgsql/))
- **`PluralKit.RedisAddr`**: the `host:port` of the Redis database to connect to

**When running using Docker, you do not need to specify the Postgres or Redis URLs as these will be overwritten by environment variables in the compose file.**

**When using Nix, the Database URI Username, Password, and Database fields must match what the database was setup with in the `flake.nix` file!**

The bot can also take configuration from environment variables, which will override the values read from the file. Here, use `:` (colon) or `__` (double underscore) as a level separator (eg. `export PluralKit__Bot__Token=foobar123`) as per [ASP.NET config](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-3.1#environment-variables).
## Nix (recommended)
The bot, databases, and services are available to run all in one as a Nix flake (using process-compose-flake). 
As of the writing of this document, there are a few caveats with the current flake file.
- The database URI in the config must match the username, password, and database specified during database creation (currently `postgres`, `postgres`, and `pluralkit` respectively).
- Not all services are added to the flake file yet.

**Additionally, as of the writing of this document, the `pluralkit-gateway` service reads environment variables to get the bot token and database URLs, so you also need to create a `.env` file in the `PluralKit` directory with the following variables:**
```
pluralkit__db__db_password="postgres"
pluralkit__db__data_db_uri="postgresql://postgres@localhost:5432/pluralkit"
pluralkit__db__data_redis_addr="redis://localhost:6379"
pluralkit__discord__bot_token="BOT_TOKEN_GOES_HERE"
pluralkit__discord__client_id="BOT_CLIENT_ID_GOES_HERE"
pluralkit__discord__client_secret=1
pluralkit__discord__max_concurrency=1
```
**(This should match the username/password/database specified in the flake file and the configuration file)**

*(assuming you already have Git installed, if not, you can start a shell with git by running `nix-shell -p git`)*
1. Clone the repository: `git clone https://github.com/PluralKit/PluralKit`
2. Create a `pluralkit.conf` configuration file in the `PluralKit` directory
	- Again, the DB URI parameters must match what's in the `flake.nix` file
3. Create a `.env` configuration file in the `PluralKit` directory *(see above)*
4. Build and run: `nix run .#dev`
	- This will download the dependencies, build, and run PluralKit
	- If Nix is not setup to allow flakes, you may need to add `--extra-experimental-features nix-command --extra-experimental-features flakes` to the command
	- If the `pluralkit-bot` process fails to run, you can restart it by selecting it and pressing `Ctrl-R`
```
[nix-shell:~]$ git clone https://github.com/PluralKit/PluralKit
[nix-shell:~]$ cd PluralKit
[nix-shell:~/PluralKit]$ cp pluralkit.conf.example pluralkit.conf
[nix-shell:~/PluralKit]$ nano pluralkit.conf
[nix-shell:~/PluralKit]$ nano .env
[nix-shell:~/PluralKit]$ nix run .#dev
```
