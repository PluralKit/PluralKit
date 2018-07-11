import logging
import os

import discord

logging.basicConfig(level=logging.DEBUG)
logging.getLogger("discord").setLevel(logging.INFO)
logging.getLogger("websockets").setLevel(logging.INFO)

logger = logging.getLogger("pluralkit.bot")
logger.setLevel(logging.DEBUG)

client = discord.Client()


@client.event
async def on_error(evt, *args, **kwargs):
    logger.exception(
        "Error while handling event {} with arguments {}:".format(evt, args))


@client.event
async def on_ready():
    # Print status info
    logger.info("Connected to Discord.")
    logger.info("Account: {}#{}".format(
        client.user.name, client.user.discriminator))
    logger.info("User ID: {}".format(client.user.id))


@client.event
async def on_message(message):
    # Ignore bot messages
    if message.author.bot:
        return

    # Split into args. shlex sucks so we don't bother with quotes
    args = message.content.split(" ")

    from pluralkit import proxy, utils

    # Find and execute command in map
    if len(args) > 0 and args[0] in utils.command_map:
        subcommand_map = utils.command_map[args[0]]

        if len(args) >= 2 and args[1] in subcommand_map:
            async with client.pool.acquire() as conn:
                await subcommand_map[args[1]][0](conn, message, args[2:])
        elif None in subcommand_map:
            async with client.pool.acquire() as conn:
                await subcommand_map[None][0](conn, message, args[1:])
        elif len(args) >= 2:
            embed = discord.Embed()
            embed.colour = discord.Colour.dark_red()
            embed.description = "Subcommand \"{}\" not found.".format(args[1])
            await client.send_message(message.channel, embed=embed)
    else:
        # Try doing proxy parsing
        async with client.pool.acquire() as conn:
            await proxy.handle_proxying(conn, message)


@client.event
async def on_reaction_add(reaction, user):
    from pluralkit import proxy

    # Pass reactions to proxy system
    async with client.pool.acquire() as conn:
        await proxy.handle_reaction(conn, reaction, user)


async def run():
    from pluralkit import db
    try:
        logger.info("Connecting to database...")
        pool = await db.connect()

        logger.info("Attempting to create tables...")
        async with pool.acquire() as conn:
            await db.create_tables(conn)

        logger.info("Connecting to InfluxDB...")

        client.pool = pool
        logger.info("Connecting to Discord...")
        await client.start(os.environ["TOKEN"])
    finally:
        logger.info("Logging out from Discord...")
        await client.logout()
