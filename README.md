# PluralKit

PluralKit is a Discord bot meant for plural communities. It has features like message proxying through webhooks, switch tracking, system and member profiles, and more.

PluralKit has a Discord server for support and discussion: https://discord.gg/PczBt78 

# Requirements
Running the bot requires Python (specifically version 3.6) and PostgreSQL.

# Configuration
Configuring the bot is done through environment variables.

* TOKEN - the Discord bot token to connect with
* CLIENT_ID - the Discord bot client ID
* DATABASE_USER - the username to log into the database with
* DATABASE_PASS - the password to log into the database with
* DATABASE_NAME - the name of the database to use
* DATABASE_HOST - the hostname of the PostgreSQL instance to connect to
* DATABASE_PORT - the port of the PostgreSQL instance to connect to
* LOG_CHANNEL (optional) - a Discord channel ID the bot will post exception tracebacks in (make this private!)

# Running

## Docker
Running PluralKit is pretty easy with Docker. The repository contains a `docker-compose.yml` file ready to use.

* Clone this repository: `git clone https://github.com/xSke/PluralKit`
* Create a `.env` file containing at least `TOKEN` and `CLIENT_ID` in `key=value` format
* Build the bot: `docker-compose build`
* Run the bot: `docker-compose up`

## Manually
You'll need to pass configuration options through shell environment variables.

* Clone this repository: `git clone https://github.com/xSke/PluralKit`
* Create a virtualenv: `virtualenv --python=python3.6 venv`
* Install dependencies: `venv/bin/pip install -r requirements.txt`
* Run PluralKit with environment variables: `TOKEN=... CLIENT_ID=... DATABASE_USER=... venv/bin/python src/bot_main.py`

# License
This project is under the Apache License, Version 2.0. It is available at the following link: https://www.apache.org/licenses/LICENSE-2.0