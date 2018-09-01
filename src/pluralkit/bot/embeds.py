from typing import Tuple

import discord


def success(text: str):
    embed = discord.Embed()
    embed.description = text
    embed.colour = discord.Colour.green()
    return embed


def error(text: str, help: Tuple[str, str] = None):
    embed = discord.Embed()
    embed.description = text
    embed.colour = discord.Colour.dark_red()

    if help:
        help_title, help_text = help
        embed.add_field(name=help_title, value=help_text)

    return embed


def status(text: str):
    embed = discord.Embed()
    embed.description = text
    embed.colour = discord.Colour.blue()
    return embed
