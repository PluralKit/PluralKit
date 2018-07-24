import logging
from datetime import datetime

import discord

from pluralkit import db


def embed_set_author_name(embed: discord.Embed, channel_name: str, member_name: str, system_name: str, avatar_url: str):
    name = "#{}: {}".format(channel_name, member_name)
    if system_name:
        name += " ({})".format(system_name)

    embed.set_author(name=name, icon_url=avatar_url or discord.Embed.Empty)


class ChannelLogger:
    def __init__(self, client: discord.Client):
        self.logger = logging.getLogger("pluralkit.bot.channel_logger")
        self.client = client

    async def get_log_channel(self, conn, server_id: str):
        server_info = await db.get_server_info(conn, server_id)

        if not server_info:
            return None

        log_channel = server_info["log_channel"]

        if not log_channel:
            return None

        return self.client.get_channel(str(log_channel))

    async def send_to_log_channel(self, log_channel: discord.Channel, embed: discord.Embed):
        try:
            await self.client.send_message(log_channel, embed=embed)
        except discord.Forbidden:
            # TODO: spew big error
            self.logger.warning(
                "Did not have permission to send message to logging channel (server={}, channel={})".format(
                    log_channel.server.id, log_channel.id))

    async def log_message_proxied(self, conn,
                                  server_id: str,
                                  channel_name: str,
                                  channel_id: str,
                                  sender_name: str,
                                  sender_disc: int,
                                  member_name: str,
                                  member_hid: str,
                                  member_avatar_url: str,
                                  system_name: str,
                                  system_hid: str,
                                  message_text: str,
                                  message_image: str,
                                  message_timestamp: datetime,
                                  message_id: str):
        log_channel = await self.get_log_channel(conn, server_id)
        if not log_channel:
            return

        embed = discord.Embed()
        embed.colour = discord.Colour.blue()
        embed.description = message_text
        embed.timestamp = message_timestamp

        embed_set_author_name(embed, channel_name, member_name, system_name, member_avatar_url)
        embed.set_footer(
            text="System ID: {} | Member ID: {} | Sender: {}#{} | Message ID: {}".format(system_hid, member_hid,
                                                                                         sender_name, sender_disc,
                                                                                         message_id))

        if message_image:
            embed.set_thumbnail(url=message_image)

        message_link = "https://discordapp.com/channels/{}/{}/{}".format(server_id, channel_id, message_id)
        embed.author.url = message_link

        await self.send_to_log_channel(log_channel, embed)

    async def log_message_deleted(self, conn,
                                  server_id: str,
                                  channel_name: str,
                                  member_name: str,
                                  member_hid: str,
                                  member_avatar_url: str,
                                  system_name: str,
                                  system_hid: str,
                                  message_text: str,
                                  message_id: str,
                                  deleted_by_moderator: bool):
        log_channel = await self.get_log_channel(conn, server_id)
        if not log_channel:
            return

        embed = discord.Embed()
        embed.colour = discord.Colour.dark_red()
        embed.description = message_text
        embed.timestamp = datetime.utcnow()

        embed_set_author_name(embed, channel_name, member_name, system_name, member_avatar_url)
        embed.set_footer(
            text="System ID: {} | Member ID: {} | Message ID: {} | Deleted by moderator? {}".format(system_hid,
                                                                                                    member_hid,
                                                                                                    message_id,
                                                                                                    "Yes" if deleted_by_moderator else "No"))

        await self.send_to_log_channel(log_channel, embed)
