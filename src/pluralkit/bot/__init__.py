import asyncio
import json
import logging
import os
import time

import traceback
from datetime import datetime

import discord

from pluralkit import db
from pluralkit.bot import channel_logger, commands, proxy, embeds
from pluralkit.stats import InfluxStatCollector, NullStatCollector

logging.basicConfig(level=logging.INFO, format="[%(asctime)s] [%(name)s] [%(levelname)s] %(message)s")


# logging.getLogger("pluralkit").setLevel(logging.DEBUG)

class PluralKitBot:
    def __init__(self, token):
        self.token = token
        self.logger = logging.getLogger("pluralkit.bot")

        self.client = discord.Client()
        self.client.event(self.on_error)
        self.client.event(self.on_ready)
        self.client.event(self.on_message)
        self.client.event(self.on_socket_raw_receive)

        self.stats = NullStatCollector()

        self.channel_logger = channel_logger.ChannelLogger(self.client)
        self.proxy = proxy.Proxy(self.client, token, self.channel_logger, self.stats)

    async def on_error(self, evt, *args, **kwargs):
        self.logger.exception("Error while handling event {} with arguments {}:".format(evt, args))

    async def on_ready(self):
        self.logger.info("Connected to Discord.")
        self.logger.info("- Account: {}#{}".format(self.client.user.name, self.client.user.discriminator))
        self.logger.info("- User ID: {}".format(self.client.user.id))
        self.logger.info("- {} servers".format(len(self.client.servers)))

    async def on_message(self, message):
        # Ignore bot messages
        if message.author.bot:
            return

        try:
            if await self.handle_command_dispatch(message):
                return

            if await self.handle_proxy_dispatch(message):
                return
        except Exception:
            await self.log_error_in_channel(message)

    async def on_socket_raw_receive(self, msg):
        # Since on_reaction_add is buggy (only works for messages the bot's already cached, ie. no old messages)
        # we parse socket data manually for the reaction add event
        if isinstance(msg, str):
            try:
                msg_data = json.loads(msg)
                if msg_data.get("t") == "MESSAGE_REACTION_ADD":
                    evt_data = msg_data.get("d")
                    if evt_data:
                        user_id = evt_data["user_id"]
                        message_id = evt_data["message_id"]
                        emoji = evt_data["emoji"]["name"]

                        async with self.pool.acquire() as conn:
                            await self.proxy.handle_reaction(conn, user_id, message_id, emoji)
                elif msg_data.get("t") == "MESSAGE_DELETE":
                    evt_data = msg_data.get("d")
                    if evt_data:
                        message_id = evt_data["id"]
                        async with self.pool.acquire() as conn:
                            await self.proxy.handle_deletion(conn, message_id)
            except ValueError:
                pass

    async def handle_command_dispatch(self, message):
        async with self.pool.acquire() as conn:
            result = await commands.command_dispatch(self.client, message, conn)
            return result

    async def handle_proxy_dispatch(self, message):
        # Try doing proxy parsing
        async with self.pool.acquire() as conn:
            return await self.proxy.try_proxy_message(conn, message)

    async def log_error_in_channel(self, message):
        channel_id = os.environ["LOG_CHANNEL"]
        if not channel_id:
            return

        channel = self.client.get_channel(channel_id)
        embed = embeds.exception_log(
            message.content,
            message.author.name,
            message.author.discriminator,
            message.server.id if message.server else None,
            message.channel.id
        )

        await self.client.send_message(channel, "```python\n{}```".format(traceback.format_exc()), embed=embed)

    async def periodical_stat_timer(self, pool):
        async with pool.acquire() as conn:
            while True:
                await self.stats.report_periodical_stats(conn)
                await asyncio.sleep(30)

    async def run(self):
        try:
            self.logger.info("Connecting to database...")
            self.pool = await db.connect(
                os.environ["DATABASE_USER"],
                os.environ["DATABASE_PASS"],
                os.environ["DATABASE_NAME"],
                os.environ["DATABASE_HOST"],
                int(os.environ["DATABASE_PORT"])
            )

            self.logger.info("Attempting to create tables...")
            async with self.pool.acquire() as conn:
                await db.create_tables(conn)

            if "INFLUX_HOST" in os.environ:
                self.logger.info("Connecting to InfluxDB...")
                self.stats = await InfluxStatCollector.connect(
                    os.environ["INFLUX_HOST"],
                    os.environ["INFLUX_PORT"],
                    os.environ["INFLUX_DB"]
                )

                self.logger.info("Starting periodical stat reporting...")
                asyncio.get_event_loop().create_task(self.periodical_stat_timer(self.pool))

            self.logger.info("Connecting to Discord...")
            await self.client.start(self.token)
        finally:
            self.logger.info("Logging out from Discord...")
            await self.client.logout()
