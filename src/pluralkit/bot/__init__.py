import asyncio
import sys

import asyncpg
import discord
import logging
import os
import traceback

from pluralkit import db
from pluralkit.bot import commands, proxy, channel_logger, embeds

logging.basicConfig(level=logging.INFO, format="[%(asctime)s] [%(name)s] [%(levelname)s] %(message)s")


def connect_to_database() -> asyncpg.pool.Pool:
    username = os.environ["DATABASE_USER"]
    password = os.environ["DATABASE_PASS"]
    name = os.environ["DATABASE_NAME"]
    host = os.environ["DATABASE_HOST"]
    port = os.environ["DATABASE_PORT"]

    if username is None or password is None or name is None or host is None or port is None:
        print(
            "Database credentials not specified. Please pass valid PostgreSQL database credentials in the DATABASE_[USER|PASS|NAME|HOST|PORT] environment variable.",
            file=sys.stderr)
        sys.exit(1)

    try:
        port = int(port)
    except ValueError:
        print("Please pass a valid integer as the DATABASE_PORT environment variable.", file=sys.stderr)
        sys.exit(1)

    return asyncio.get_event_loop().run_until_complete(db.connect(
        username=username,
        password=password,
        database=name,
        host=host,
        port=port
    ))


def run():
    pool = connect_to_database()

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
        log_channel_id = os.environ["LOG_CHANNEL"]
        if not log_channel_id:
            return

        log_channel = client.get_channel(int(log_channel_id))

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

        # Print it to stderr anyway, though
        logging.getLogger("pluralkit").exception("Exception while handling event {}".format(event_name))

    bot_token = os.environ["TOKEN"]
    if not bot_token:
        print("No token specified. Please pass a valid Discord bot token in the TOKEN environment variable.",
              file=sys.stderr)
        sys.exit(1)

    client.run(bot_token)
