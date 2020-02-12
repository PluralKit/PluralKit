# PluralKit
PluralKit is a Discord bot meant for plural communities. It has features like message proxying through webhooks, switch tracking, system and member profiles, and more.

**Do you just want to add PluralKit to your server? If so, you don't need any of this. Use the bot's invite link: https://discordapp.com/oauth2/authorize?client_id=466378653216014359&scope=bot&permissions=536995904**

PluralKit has a Discord server for support, feedback, and discussion: https://discord.gg/PczBt78 

# Requirements
Running the bot requires [.NET Core](https://dotnet.microsoft.com/download) (v3.1) and a PostgreSQL database.

# Configuration
Configuring the bot is done through a JSON configuration file. An example of the configuration format can be seen in [`pluralkit.conf.example`](https://github.com/xSke/PluralKit/blob/master/pluralkit.conf.example).
The configuration file needs to be placed in the bot's working directory (usually the repository root) and must be called `pluralkit.conf`.

The configuration file is in JSON format (albeit with a `.conf` extension), and the following keys (using `.` to indicate a nested object level) are available:

The following keys are available:
* `PluralKit.Database`: the URI of the database to connect to (in [ADO.NET Npgsql format](https://www.connectionstrings.com/npgsql/))
* `PluralKit.Bot.Token`: the Discord bot token to connect with

# Running

## Docker
Running PluralKit is pretty easy with Docker. The repository contains a `docker-compose.yml` file ready to use.

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
* Install the .NET Core 3.1 SDK (see https://dotnet.microsoft.com/download)
* Clone this repository: `git clone https://github.com/xSke/PluralKit`
* Create and fill in a `pluralkit.conf` file in the same directory as `docker-compose.yml`
* Run the bot: `dotnet run --project PluralKit.Bot`

# Building the docs
The website and documentation are automatically built by GitHub Pages when pushed to the `master` branch. They use [Jekyll 3](https://jekyllrb.com), which requires [Ruby](https://www.ruby-lang.org) and [Bundler](https://bundler.io/).

To build the docs locally, run:
```
$ cd docs/
$ bundle install --path vendor/bundle
$ bundle exec jekyll build
```

To run an auto-reloading server, substitute the last command with:

    $ bundle exec jekyll serve 

# License
This project is under the Apache License, Version 2.0. It is available at the following link: https://www.apache.org/licenses/LICENSE-2.0