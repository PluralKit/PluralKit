import asyncio
import sys

import asyncpg
from collections import namedtuple
import discord
import logging
import json
import os
import traceback

from pluralkit import db
from pluralkit.bot import commands, proxy, channel_logger, embeds

logging.basicConfig(level=logging.INFO, format="[%(asctime)s] [%(name)s] [%(levelname)s] %(message)s")

class Config(namedtuple("Config", ["database_uri", "token", "log_channel"])):
    required_fields = ["database_uri", "token"]

    database_uri: str
    token: str
    log_channel: str

    @staticmethod
    def from_file_and_env(filename: str) -> "Config":
        try:
            with open(filename, "r") as f:
                config = json.load(f)
        except IOError as e:
            # If all the required fields are specified as environment variables, it's OK to 
            # not raise the IOError, we can just construct the dict from these
            if all([rf.upper() in os.environ for rf in Config.required_fields]):
                config = {}
            else:
                # If they aren't, though, then rethrow
                raise e

        # Override with environment variables
        for f in Config._fields:
            if f.upper() in os.environ:
                config[f] = os.environ[f.upper()]

        # If we currently don't have all the required fields, then raise
        if not all([rf in config for rf in Config.required_fields]):
            raise RuntimeError("Some required config fields were missing: " + ", ".join(filter(lambda rf: rf not in config, Config.required_fields)))

        return Config(**config)


def connect_to_database(uri: str) -> asyncpg.pool.Pool:
    return asyncio.get_event_loop().run_until_complete(db.connect(uri))


def run(config: Config):
    pool = connect_to_database(config.database_uri)

    async def create_tables():
        async with pool.acquire() as conn:
            await db.create_tables(conn)

    asyncio.get_event_loop().run_until_complete(create_tables())

    client = discord.Client()
    logger = channel_logger.ChannelLogger(client)

    @client.event
    async def on_ready():
        print("PluralKit started.")
        print("User: {}#{} (ID: {})".format(client.user.name, client.user.discriminator, client.user.id))
        print("{} servers".format(len(client.guilds)))
        print("{} shards".format(client.shard_count or 1))

        await client.change_presence(activity=discord.Game(name="pk;help"))

    @client.event
    async def on_message(message: discord.Message):
        # Ignore messages from bots
        if message.author.bot:
            return

        # Grab a database connection from the pool
        async with pool.acquire() as conn:
            # First pass: do command handling
            did_run_command = await commands.command_dispatch(client, message, conn)
            if did_run_command:
                return

            # Second pass: do proxy matching
            await proxy.try_proxy_message(conn, message, logger, client.user)

    @client.event
    async def on_raw_message_delete(payload: discord.RawMessageDeleteEvent):
        async with pool.acquire() as conn:
            await proxy.handle_deleted_message(conn, client, payload.message_id, None, logger)

    @client.event
    async def on_raw_bulk_message_delete(payload: discord.RawBulkMessageDeleteEvent):
        async with pool.acquire() as conn:
            for message_id in payload.message_ids:
                await proxy.handle_deleted_message(conn, client, message_id, None, logger)

    @client.event
    async def on_raw_reaction_add(payload: discord.RawReactionActionEvent):
        if payload.emoji.name == "\u274c":  # Red X
            async with pool.acquire() as conn:
                await proxy.try_delete_by_reaction(conn, client, payload.message_id, payload.user_id, logger)

    @client.event
    async def on_error(event_name, *args, **kwargs):
        # Print it to stderr
        logging.getLogger("pluralkit").exception("Exception while handling event {}".format(event_name))

        # Then log it to the given log channel
        # TODO: replace this with Sentry or something
        if not config.log_channel:
            return
        log_channel = client.get_channel(int(config.log_channel))

        # If this is a message event, we can attach additional information in an event
        # ie. username, channel, content, etc
        if args and isinstance(args[0], discord.Message):
            message: discord.Message = args[0]
            embed = embeds.exception_log(
                message.content,
                message.author.name,
                message.author.discriminator,
                message.author.id,
                message.guild.id if message.guild else None,
                message.channel.id
            )
        else:
            # If not, just post the string itself
            embed = None

        traceback_str = "```python\n{}```".format(traceback.format_exc())
        if len(traceback.format_exc()) >= (2000 - len("```python\n```")):
            traceback_str = "```python\n...{}```".format(traceback.format_exc()[- (2000 - len("```python\n...```")):])
        await log_channel.send(content=traceback_str, embed=embed)
    client.run(config.token)
