# PluralKit
PluralKit is a Discord bot meant for plural communities. It has features like message proxying through webhooks, switch tracking, system and member profiles, and more.

**Do you just want to add PluralKit to your server? If so, you don't need any of this. Use the bot's invite link: https://discord.com/oauth2/authorize?client_id=466378653216014359&scope=bot%20applications.commands&permissions=536995904**

PluralKit has a Discord server for support, feedback, and discussion: https://discord.gg/PczBt78 

# Requirements
Running the bot requires [.NET 5](https://dotnet.microsoft.com/download) and a PostgreSQL database. It should function on any system where the prerequisites are set up (including Windows).

Optionally, it can integrate with [Sentry](https://sentry.io/welcome/) for error reporting and [InfluxDB](https://www.influxdata.com/products/influxdb-overview/) for aggregate statistics.

# Configuration
Configuring the bot is done through a JSON configuration file. An example of the configuration format can be seen in [`pluralkit.conf.example`](https://github.com/xSke/PluralKit/blob/master/pluralkit.conf.example).
The configuration file needs to be placed in the bot's working directory (usually the repository root) and must be called `pluralkit.conf`.

The configuration file is in JSON format (albeit with a `.conf` extension). The following keys are available (using `.` to indicate a nested object level), bolded key names are required:
* **`PluralKit.Bot.Token`**: the Discord bot token to connect with
* **`PluralKit.Database`**: the URI of the database to connect to (in [ADO.NET Npgsql format](https://www.connectionstrings.com/npgsql/))
* `PluralKit.Bot.Prefixes`: an array of command prefixes to use (default `["pk;", "pk!"]`).
* `PluralKit.Bot.ClientId` *(optional)*: the ID of the bot's user account, used when generating invite links through `pk;invite`. It's automatically determined if not present, but overriding it may be useful for private instances that still want a public invite link.
* `PluralKit.SentryUrl` *(optional)*: the [Sentry](https://sentry.io/welcome/) client key/DSN to report runtime errors to. If absent, disables Sentry integration.
* `PluralKit.InfluxUrl` *(optional)*: the URL to an [InfluxDB](https://www.influxdata.com/products/influxdb-overview/) server to report aggregate statistics to. An example of these stats can be seen on [the public stats page](https://stats.pluralkit.me). 
* `PluralKit.InfluxDb` *(optional)*: the name of an InfluxDB database to report statistics to. If either this field or `PluralKit.InfluxUrl` are absent, InfluxDB reporting will be disabled.
* `PluralKit.LogDir` *(optional)*: the directory to save information and error logs to. If left blank, will default to `logs/` in the current working directory.

The bot can also take configuration from environment variables, which will override the values read from the file. Here, use `:` (colon) or `__` (double underscore) as a level separator (eg. `export PluralKit__Bot__Token=foobar123`) as per [ASP.NET config](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-3.1#environment-variables).

# Running

## Docker
The easiest way to get the bot running is with Docker. The repository contains a `docker-compose.yml` file ready to use.

* Clone this repository: `git clone https://github.com/xSke/PluralKit`
* Create a `pluralkit.conf` file in the same directory as `docker-compose.yml` containing at least a `PluralKit.Bot.Token` field
  * (`PluralKit.Database` is overridden in `docker-compose.yml` to point to the Postgres container)
* Build the bot: `docker-compose build`
* Run the bot: `docker-compose up`

In other words:
```
$ git clone https://github.com/xSke/PluralKit
$ cd PluralKit
$ cp pluralkit.conf.example pluralkit.conf
$ nano pluralkit.conf  # (or vim, or whatever)
$ docker-compose up -d
```

## Manually
* Install the .NET 6 SDK (see https://dotnet.microsoft.com/download)
* Clone this repository: `git clone https://github.com/xSke/PluralKit`
* Create and fill in a `pluralkit.conf` file in the same directory as `docker-compose.yml`
* Run the bot: `dotnet run --project PluralKit.Bot`
  * Alternatively, `dotnet build -c Release -o build/`, then `dotnet build/PluralKit.Bot.dll`

(tip: use `scripts/run-test-db.sh` to run a temporary PostgreSQL database on your local system. Requires Docker.)

## Scheduled Tasks worker

There is a scheduled tasks worker that needs to be ran separately from the bot. This handles cleaning up the database, and updating statistics (system/member/etc counts, shown in the `pk;stats` embed).

Note: This worker is *not required*, and the bot will function correctly without it.

If you are running the bot via docker-compose, this is set up automatically.

If you run the bot manually you can run the worker as such:
* `dotnet run --project PluralKit.ScheduledTasks`
* or if you used `dotnet build` rather than `dotnet run` to run the bot: `dotnet build/PluralKit.ScheduledTasks.dll`

# Upgrading database from legacy version
If you have an instance of the Python version of the bot (from the `legacy` branch), you may need to take extra database migration steps.
For more information, see [LEGACYMIGRATE.md](./LEGACYMIGRATE.md).

# Documentation
See [the docs/ directory](./docs/README.md)

# License
This project is under the GNU Affero General Public License, Version 3. It is available at the following link: https://www.gnu.org/licenses/agpl-3.0.en.html