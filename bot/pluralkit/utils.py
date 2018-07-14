import random
import re
import string

import asyncio
import asyncpg
import discord
import humanize

from pluralkit import db
from pluralkit.bot import client, logger

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

async def parse_mention(mention: str) -> discord.User:
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

async def get_fronter_ids(conn, system_id):
    switches = await db.front_history(conn, system_id=system_id, count=1)
    if not switches:
        return [], None
    
    if not switches[0]["members"]:
        return [], switches[0]["timestamp"]
    
    return switches[0]["members"], switches[0]["timestamp"]

async def get_fronters(conn, system_id):
    member_ids, timestamp = await get_fronter_ids(conn, system_id)

    # Collect in dict and then look up as list, to preserve return order
    members = {member["id"]: member for member in await db.get_members(conn, member_ids)}
    return [members[member_id] for member_id in member_ids], timestamp

async def get_front_history(conn, system_id, count):
    # Get history from DB
    switches = await db.front_history(conn, system_id=system_id, count=count)
    if not switches:
        return []
    
    # Get all unique IDs referenced
    all_member_ids = {id for switch in switches for id in switch["members"]}
    
    # And look them up in the database into a dict
    all_members = {member["id"]: member for member in await db.get_members(conn, list(all_member_ids))}

    # Collect in array and return
    out = []
    for switch in switches:
        timestamp = switch["timestamp"]
        members = [all_members[id] for id in switch["members"]]
        out.append((timestamp, members))
    return out

async def get_system_fuzzy(conn, key) -> asyncpg.Record:
    if isinstance(key, discord.User):
        return await db.get_system_by_account(conn, account_id=key.id)

    if isinstance(key, str) and len(key) == 5:
        return await db.get_system_by_hid(conn, system_hid=key)

    account = await parse_mention(key)
    if account:
        system = await db.get_system_by_account(conn, account_id=account.id)
        if system:
            return system
    return None


async def get_member_fuzzy(conn, system_id: int, key: str, system_only=True) -> asyncpg.Record:
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

command_map = {}

# Command wrapper
# Return True for success, return False for failure
# Second parameter is the message it'll send. If just False, will print usage


def command(cmd, usage=None, description=None, category=None):
    def wrap(func):
        async def wrapper(conn, message, args):
            res = await func(conn, message, args)

            if res is not None:
                if not isinstance(res, tuple):
                    success, msg = res, None
                else:
                    success, msg = res

                if not success and not msg:
                    # Failure, no message, print usage
                    usage_str = "**Usage:** {} {}".format(cmd, usage or "")
                    await client.send_message(message.channel, embed=make_default_embed(usage_str))
                elif not success:
                    # Failure, print message
                    embed = msg if isinstance(msg, discord.Embed) else make_error_embed(msg)
                    # embed.set_footer(text="{:.02f} ms".format(time_ms))
                    await client.send_message(message.channel, embed=embed)
                elif msg:
                    # Success, print message
                    embed = msg if isinstance(msg, discord.Embed) else make_default_embed(msg)
                    # embed.set_footer(text="{:.02f} ms".format(time_ms))
                    await client.send_message(message.channel, embed=embed)
                # Success, don't print anything

        # Put command in map
        command_map[cmd] = (wrapper, usage, description, category)
        return wrapper
    return wrap

# Member command wrapper
# Tries to find member by first argument
# If system_only=False, allows members from other systems by hid


def member_command(cmd, usage=None, description=None, category=None, system_only=True):
    def wrap(func):
        async def wrapper(conn, message, args):
            # Return if no member param
            if len(args) == 0:
                return False

            # If system_only, we need a system to check
            system = await db.get_system_by_account(conn, message.author.id)
            if system_only and system is None:
                return False, "No system is registered to this account."

            # System is allowed to be none if not system_only
            system_id = system["id"] if system else None
            # And find member by key
            member = await get_member_fuzzy(conn, system_id=system_id, key=args[0], system_only=system_only)

            if member is None:
                return False, "Can't find member \"{}\".".format(args[0])

            return await func(conn, message, member, args[1:])
        return command(cmd=cmd, usage="<name|id> {}".format(usage or ""), description=description, category=category)(wrapper)
    return wrap


async def generate_system_info_card(conn, system: asyncpg.Record) -> discord.Embed:
    card = discord.Embed()
    card.colour = discord.Colour.blue()

    if system["name"]:
        card.title = system["name"]

    if system["tag"]:
        card.add_field(name="Tag", value=system["tag"])
    
    fronters, switch_time = await get_fronters(conn, system["id"])
    if fronters:
        names = ", ".join([member["name"] for member in fronters])
        fronter_val = "{} (for {})".format(names, humanize.naturaldelta(switch_time))
        card.add_field(name="Current fronter" if len(fronters) == 1 else "Current fronters", value=fronter_val)

    account_names = []
    for account_id in await db.get_linked_accounts(conn, system_id=system["id"]):
        account = await client.get_user_info(account_id)
        account_names.append("{}#{}".format(account.name, account.discriminator))
    card.add_field(name="Linked accounts", value="\n".join(account_names))
    
    if system["description"]:
        card.add_field(name="Description",
                       value=system["description"], inline=False)

    # Get names of all members
    member_texts = []
    for member in await db.get_all_members(conn, system_id=system["id"]):
        member_texts.append("{} (`{}`)".format(escape(member["name"]), member["hid"]))

    if len(member_texts) > 0:
        card.add_field(name="Members", value="\n".join(
            member_texts), inline=False)

    card.set_footer(text="System ID: {}".format(system["hid"]))
    return card


async def generate_member_info_card(conn, member: asyncpg.Record) -> discord.Embed:
    system = await db.get_system(conn, system_id=member["system"])

    card = discord.Embed()
    card.colour = discord.Colour.blue()

    name_and_system = member["name"]
    if system["name"]:
        name_and_system += " ({})".format(system["name"])

    card.set_author(name=name_and_system, icon_url=member["avatar_url"] or discord.Embed.Empty)
    if member["avatar_url"]:
        card.set_thumbnail(url=member["avatar_url"])

    # Get system name and hid
    system = await db.get_system(conn, system_id=member["system"])

    if member["color"]:
        card.colour = int(member["color"], 16)

    if member["birthday"]:
        bday_val = member["birthday"].strftime("%b %d, %Y")
        if member["birthday"].year < 1000:
            bday_val = member["birthday"].strftime("%b %d")
        card.add_field(name="Birthdate", value=bday_val)

    if member["pronouns"]:
        card.add_field(name="Pronouns", value=member["pronouns"])

    if member["prefix"] or member["suffix"]:
        prefix = member["prefix"] or ""
        suffix = member["suffix"] or ""
        card.add_field(name="Proxy Tags",
                       value="{}text{}".format(prefix, suffix))

    if member["description"]:
        card.add_field(name="Description",
                       value=member["description"], inline=False)

    card.set_footer(text="System ID: {} | Member ID: {}".format(
        system["hid"], member["hid"]))
    return card


async def text_input(message, subject):
    embed = make_default_embed("")
    embed.description = "Reply in this channel with the new description you want to set for {}.".format(subject)

    status_msg = await client.send_message(message.channel, embed=embed)
    reply_msg = await client.wait_for_message(author=message.author, channel=message.channel)

    embed.description = "Alright. When you're happy with the new description, click the ✅ reaction. To cancel, click the ❌ reaction."
    await client.edit_message(status_msg, embed=embed)
    await client.add_reaction(reply_msg, "✅")
    await client.add_reaction(reply_msg, "❌")

    reaction = await client.wait_for_reaction(emoji=["✅", "❌"], message=reply_msg, user=message.author)
    if reaction.reaction.emoji == "✅":
        await client.clear_reactions(reply_msg)
        return reply_msg.content
    else:
        await client.clear_reactions(reply_msg)
        return None
