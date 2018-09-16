from typing import Tuple

import discord


def success(text: str) -> discord.Embed:
    embed = discord.Embed()
    embed.description = text
    embed.colour = discord.Colour.green()
    return embed


def error(text: str, help: Tuple[str, str] = None) -> discord.Embed:
    embed = discord.Embed()
    embed.description = text
    embed.colour = discord.Colour.dark_red()

    if help:
        help_title, help_text = help
        embed.add_field(name=help_title, value=help_text)

    return embed


def status(text: str) -> discord.Embed:
    embed = discord.Embed()
    embed.description = text
    embed.colour = discord.Colour.blue()
    return embed


def exception_log(message_content, author_name, author_discriminator, server_id, channel_id) -> discord.Embed:
    embed = discord.Embed()
    embed.colour = discord.Colour.dark_red()
    embed.title = message_content

    embed.set_footer(text="Sender: {}#{} | Server: {} | Channel: {}".format(
        author_name, author_discriminator,
        server_id if server_id else "(DMs)",
        channel_id
    ))
    return embed