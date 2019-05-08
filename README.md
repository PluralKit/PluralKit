# PluralKit

PluralKit is a Discord bot meant for plural communities. It has features like message proxying through webhooks, switch tracking, system and member profiles, and more.

PluralKit has a Discord server for support and discussion: https://discord.gg/PczBt78 

# Requirements
Running the bot requires Python (specifically version 3.6) and PostgreSQL.

# Configuration
Configuring the bot is done through a configuration file. An example of the configuration format can be seen below, in the Example PluralKit Configuration section.

The following keys are available:
* `token`: the Discord bot token to connect with
* `database_uri`: the URI of the database to connect to (format: `postgres://username:password@hostname:port/database_name`)
* `log_channel` (optional): a Discord channel ID the bot will post exception tracebacks in (make this private!)

The environment variables `TOKEN` and `DATABASE_URI` will override the configuration file values when present.


# Example PluralKit Configuration
```
{
    "database_uri": "postgres://username:password@hostname:port/database_name",
    "token": "BOT_TOKEN_GOES_HERE",
    "log_channel": null
}
```

# Running

## Docker
Running PluralKit is pretty easy with Docker. The repository contains a `docker-compose.yml` file ready to use.

* Clone this repository: `git clone https://github.com/xSke/PluralKit`
* Create a `pluralkit.conf` file in the same directory as `docker-compose.yml` containing at least a `token` field
* Build the bot: `docker-compose build`
* Run the bot: `docker-compose up`

## Manually
* Clone this repository: `git clone https://github.com/xSke/PluralKit`
* Create a virtualenv: `virtualenv --python=python3.6 venv`
* Install dependencies: `venv/bin/pip install -r requirements.txt`
* Run PluralKit with the config file: `venv/bin/python src/bot_main.py`
  * The bot optionally takes a parameter describing the location of the configuration file, defaulting to `./pluralkit.conf`.

# License
This project is under the Apache License, Version 2.0. It is available at the following link: https://www.apache.org/licenses/LICENSE-2.0
