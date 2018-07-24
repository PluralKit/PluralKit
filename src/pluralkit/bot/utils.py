from datetime import datetime
import logging
import random
import re
from typing import List, Tuple
import string

import asyncio
import asyncpg
import discord
import humanize

from pluralkit import System, Member, db

logger = logging.getLogger("pluralkit.utils")

def escape(s):
    return s.replace("`", "\\`")

def generate_hid() -> str:
    return "".join(random.choices(string.ascii_lowercase, k=5))

def bounds_check_member_name(new_name, system_tag):
    if len(new_name) > 32:
        return "Name cannot be longer than 32 characters."

    if system_tag:
        if len("{} {}".format(new_name, system_tag)) > 32:
            return "This name, combined with the system tag ({}), would exceed the maximum length of 32 characters. Please reduce the length of the tag, or use a shorter name.".format(system_tag)

async def parse_mention(client: discord.Client, mention: str) -> discord.User:
    # First try matching mention format
    match = re.fullmatch("<@!?(\\d+)>", mention)
    if match:
        try:
            return await client.get_user_info(match.group(1))
        except discord.NotFound:
            return None

    # Then try with just ID
    try:
        return await client.get_user_info(str(int(mention)))
    except (ValueError, discord.NotFound):
        return None

def parse_channel_mention(mention: str, server: discord.Server) -> discord.Channel:
    match = re.fullmatch("<#(\\d+)>", mention)
    if match:
        return server.get_channel(match.group(1))
    
    try:
        return server.get_channel(str(int(mention)))
    except ValueError:
        return None

async def get_fronter_ids(conn, system_id) -> (List[int], datetime):
    switches = await db.front_history(conn, system_id=system_id, count=1)
    if not switches:
        return [], None
    
    if not switches[0]["members"]:
        return [], switches[0]["timestamp"]
    
    return switches[0]["members"], switches[0]["timestamp"]

async def get_fronters(conn, system_id) -> (List[Member], datetime):
    member_ids, timestamp = await get_fronter_ids(conn, system_id)

    # Collect in dict and then look up as list, to preserve return order
    members = {member.id: member for member in await db.get_members(conn, member_ids)}
    return [members[member_id] for member_id in member_ids], timestamp

async def get_front_history(conn, system_id, count) -> List[Tuple[datetime, List[Member]]]:
    # Get history from DB
    switches = await db.front_history(conn, system_id=system_id, count=count)
    if not switches:
        return []
    
    # Get all unique IDs referenced
    all_member_ids = {id for switch in switches for id in switch["members"]}
    
    # And look them up in the database into a dict
    all_members = {member.id: member for member in await db.get_members(conn, list(all_member_ids))}

    # Collect in array and return
    out = []
    for switch in switches:
        timestamp = switch["timestamp"]
        members = [all_members[id] for id in switch["members"]]
        out.append((timestamp, members))
    return out

async def get_system_fuzzy(conn, client: discord.Client, key) -> System:
    if isinstance(key, discord.User):
        return await db.get_system_by_account(conn, account_id=key.id)

    if isinstance(key, str) and len(key) == 5:
        return await db.get_system_by_hid(conn, system_hid=key)

    account = await parse_mention(client, key)
    if account:
        system = await db.get_system_by_account(conn, account_id=account.id)
        if system:
            return system
    return None


async def get_member_fuzzy(conn, system_id: int, key: str, system_only=True) -> Member:
    # First search by hid
    if system_only:
        member = await db.get_member_by_hid_in_system(conn, system_id=system_id, member_hid=key)
    else:
        member = await db.get_member_by_hid(conn, member_hid=key)
    if member is not None:
        return member

    # Then search by name, if we have a system
    if system_id:
        member = await db.get_member_by_name(conn, system_id=system_id, member_name=key)
        if member is not None:
            return member

def make_default_embed(message):
    embed = discord.Embed()
    embed.colour = discord.Colour.blue()
    embed.description = message
    return embed

def make_error_embed(message):
    embed = discord.Embed()
    embed.colour = discord.Colour.dark_red()
    embed.description = message
    return embed


async def generate_system_info_card(conn, client: discord.Client, system: System) -> discord.Embed:
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


async def generate_member_info_card(conn, member: Member) -> discord.Embed:
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

    if member.prefix or member.suffix:
        prefix = member.prefix or ""
        suffix = member.suffix or ""
        card.add_field(name="Proxy Tags",
                       value="{}text{}".format(prefix, suffix))

    if member.description:
        card.add_field(name="Description",
                       value=member.description, inline=False)

    card.set_footer(text="System ID: {} | Member ID: {}".format(
        system.hid, member.hid))
    return card