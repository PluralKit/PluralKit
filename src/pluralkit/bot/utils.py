import logging
import re

import discord
from typing import Optional

from pluralkit import db
from pluralkit.system import System
from pluralkit.member import Member

logger = logging.getLogger("pluralkit.utils")

def escape(s):
    return s.replace("`", "\\`")

def bounds_check_member_name(new_name, system_tag):
    if len(new_name) > 32:
        return "Name cannot be longer than 32 characters."

    if system_tag:
        if len("{} {}".format(new_name, system_tag)) > 32:
            return "This name, combined with the system tag ({}), would exceed the maximum length of 32 characters. Please reduce the length of the tag, or use a shorter name.".format(system_tag)

async def parse_mention(client: discord.Client, mention: str) -> Optional[discord.User]:
    # First try matching mention format
    match = re.fullmatch("<@!?(\\d+)>", mention)
    if match:
        try:
            return await client.get_user_info(int(match.group(1)))
        except discord.NotFound:
            return None

    # Then try with just ID
    try:
        return await client.get_user_info(int(mention))
    except (ValueError, discord.NotFound):
        return None

def parse_channel_mention(mention: str, server: discord.Guild) -> Optional[discord.TextChannel]:
    match = re.fullmatch("<#(\\d+)>", mention)
    if match:
        return server.get_channel(int(match.group(1)))
    
    try:
        return server.get_channel(int(mention))
    except ValueError:
        return None


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

def sanitize(text):
    # Insert a zero-width space in @everyone so it doesn't trigger
    return text.replace("@everyone", "@\u200beveryone").replace("@here", "@\u200bhere")