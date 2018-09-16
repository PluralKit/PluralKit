import humanize
from typing import Tuple

import discord

from pluralkit import db
from pluralkit.bot.utils import escape
from pluralkit.member import Member
from pluralkit.system import System
from pluralkit.utils import get_fronters


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


async def system_card(conn, client: discord.Client, system: System) -> discord.Embed:
    card = discord.Embed()
    card.colour = discord.Colour.blue()

    if system.name:
        card.title = system.name

    if system.avatar_url:
        card.set_thumbnail(url=system.avatar_url)

    if system.tag:
        card.add_field(name="Tag", value=system.tag)

    fronters, switch_time = await get_fronters(conn, system.id)
    if fronters:
        names = ", ".join([member.name for member in fronters])
        fronter_val = "{} (for {})".format(names, humanize.naturaldelta(switch_time))
        card.add_field(name="Current fronter" if len(fronters) == 1 else "Current fronters", value=fronter_val)

    account_names = []
    for account_id in await db.get_linked_accounts(conn, system_id=system.id):
        account = await client.get_user_info(account_id)
        account_names.append("{}#{}".format(account.name, account.discriminator))
    card.add_field(name="Linked accounts", value="\n".join(account_names))

    if system.description:
        card.add_field(name="Description",
                       value=system.description, inline=False)

    # Get names of all members
    member_texts = []
    for member in await db.get_all_members(conn, system_id=system.id):
        member_texts.append("{} (`{}`)".format(escape(member.name), member.hid))

    if len(member_texts) > 0:
        card.add_field(name="Members", value="\n".join(
            member_texts), inline=False)

    card.set_footer(text="System ID: {}".format(system.hid))
    return card


async def member_card(conn, member: Member) -> discord.Embed:
    system = await db.get_system(conn, system_id=member.system)

    card = discord.Embed()
    card.colour = discord.Colour.blue()

    name_and_system = member.name
    if system.name:
        name_and_system += " ({})".format(system.name)

    card.set_author(name=name_and_system, icon_url=member.avatar_url or discord.Embed.Empty)
    if member.avatar_url:
        card.set_thumbnail(url=member.avatar_url)

    # Get system name and hid
    system = await db.get_system(conn, system_id=member.system)

    if member.color:
        card.colour = int(member.color, 16)

    if member.birthday:
        bday_val = member.birthday.strftime("%b %d, %Y")
        if member.birthday.year == 1:
            bday_val = member.birthday.strftime("%b %d")
        card.add_field(name="Birthdate", value=bday_val)

    if member.pronouns:
        card.add_field(name="Pronouns", value=member.pronouns)

    message_count = await db.get_member_message_count(conn, member.id)
    if message_count > 0:
        card.add_field(name="Message Count", value=str(message_count), inline=True)

    if member.prefix or member.suffix:
        prefix = member.prefix or ""
        suffix = member.suffix or ""
        card.add_field(name="Proxy Tags",
                       value="{}text{}".format(prefix, suffix))

    if member.description:
        card.add_field(name="Description",
                       value=member.description, inline=False)

    card.set_footer(text="System ID: {} | Member ID: {}".format(system.hid, member.hid))
    return card