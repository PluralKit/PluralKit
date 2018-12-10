import discord
import humanize
from typing import Tuple

from pluralkit import db
from pluralkit.bot.utils import escape
from pluralkit.member import Member
from pluralkit.switch import Switch
from pluralkit.system import System
from pluralkit.utils import get_fronters, display_relative


def truncate_field_name(s: str) -> str:
    return s[:256]


def truncate_field_body(s: str) -> str:
    return s[:1024]


def truncate_description(s: str) -> str:
    return s[:2048]


def truncate_title(s: str) -> str:
    return s[:256]


def success(text: str) -> discord.Embed:
    embed = discord.Embed()
    embed.description = truncate_description(text)
    embed.colour = discord.Colour.green()
    return embed


def error(text: str, help: Tuple[str, str] = None) -> discord.Embed:
    embed = discord.Embed()
    embed.description = truncate_description(text)
    embed.colour = discord.Colour.dark_red()

    if help:
        help_title, help_text = help
        embed.add_field(name=truncate_field_name(help_title), value=truncate_field_body(help_text))

    return embed


def status(text: str) -> discord.Embed:
    embed = discord.Embed()
    embed.description = truncate_description(text)
    embed.colour = discord.Colour.blue()
    return embed


def exception_log(message_content, author_name, author_discriminator, author_id, server_id,
                  channel_id) -> discord.Embed:
    embed = discord.Embed()
    embed.colour = discord.Colour.dark_red()
    embed.title = truncate_title(message_content)

    embed.set_footer(text="Sender: {}#{} ({}) | Server: {} | Channel: {}".format(
        author_name, author_discriminator, author_id,
        server_id if server_id else "(DMs)",
        channel_id
    ))
    return embed


async def system_card(conn, client: discord.Client, system: System) -> discord.Embed:
    card = discord.Embed()
    card.colour = discord.Colour.blue()

    if system.name:
        card.title = truncate_title(system.name)

    if system.avatar_url:
        card.set_thumbnail(url=system.avatar_url)

    if system.tag:
        card.add_field(name="Tag", value=truncate_field_body(system.tag))

    fronters, switch_time = await get_fronters(conn, system.id)
    if fronters:
        names = ", ".join([member.name for member in fronters])
        fronter_val = "{} (for {})".format(names, humanize.naturaldelta(switch_time))
        card.add_field(name="Current fronter" if len(fronters) == 1 else "Current fronters",
                       value=truncate_field_body(fronter_val))

    account_names = []
    for account_id in await system.get_linked_account_ids(conn):
        account = await client.get_user_info(account_id)
        account_names.append("{}#{}".format(account.name, account.discriminator))

    card.add_field(name="Linked accounts", value=truncate_field_body("\n".join(account_names)))

    if system.description:
        card.add_field(name="Description",
                       value=truncate_field_body(system.description), inline=False)

    # Get names of all members
    all_members = await system.get_members(conn)
    if all_members:
        member_texts = []
        for member in all_members:
            member_texts.append("{} (`{}`)".format(escape(member.name), member.hid))

        # Interim solution for pagination of large systems
        # Previously a lot of systems would hit the 1024 character limit and thus break the message
        # This splits large system lists into multiple embed fields
        # The 6000 character total limit will still apply here but this sort of pushes the problem until I find a better fix
        pages = [""]
        for member in member_texts:
            last_page = pages[-1]
            new_page = last_page + "\n" + member if last_page else member

            if len(new_page) >= 1024:
                pages.append(member)
            else:
                pages[-1] = new_page

        for index, page in enumerate(pages):
            field_name = "Members"
            if index >= 1:
                field_name = "Members (part {})".format(index + 1)
            card.add_field(name=truncate_field_name(field_name), value=truncate_field_body(page), inline=False)

    card.set_footer(text="System ID: {}".format(system.hid))
    return card


async def member_card(conn, member: Member) -> discord.Embed:
    system = await member.fetch_system(conn)

    card = discord.Embed()
    card.colour = discord.Colour.blue()

    name_and_system = member.name
    if system.name:
        name_and_system += " ({})".format(system.name)

    card.set_author(name=truncate_field_name(name_and_system), icon_url=member.avatar_url or discord.Embed.Empty)
    if member.avatar_url:
        card.set_thumbnail(url=member.avatar_url)

    if member.color:
        card.colour = int(member.color, 16)

    if member.birthday:
        bday_val = member.birthday.strftime("%b %d, %Y")
        if member.birthday.year == 1:
            bday_val = member.birthday.strftime("%b %d")
        card.add_field(name="Birthdate", value=bday_val)

    if member.pronouns:
        card.add_field(name="Pronouns", value=truncate_field_body(member.pronouns))

    message_count = await member.message_count(conn)
    if message_count > 0:
        card.add_field(name="Message Count", value=str(message_count), inline=True)

    if member.prefix or member.suffix:
        prefix = member.prefix or ""
        suffix = member.suffix or ""
        card.add_field(name="Proxy Tags",
                       value=truncate_field_body("{}text{}".format(prefix, suffix)))

    if member.description:
        card.add_field(name="Description",
                       value=truncate_field_body(member.description), inline=False)

    card.set_footer(text="System ID: {} | Member ID: {}".format(system.hid, member.hid))
    return card


async def front_status(switch: Switch, conn) -> discord.Embed:
    if switch:
        embed = status("")
        fronter_names = [member.name for member in await switch.fetch_members(conn)]

        if len(fronter_names) == 0:
            embed.add_field(name="Current fronter", value="(no fronter)")
        elif len(fronter_names) == 1:
            embed.add_field(name="Current fronter", value=truncate_field_body(fronter_names[0]))
        else:
            embed.add_field(name="Current fronters", value=truncate_field_body(", ".join(fronter_names)))

        if switch.timestamp:
            embed.add_field(name="Since",
                            value="{} ({})".format(switch.timestamp.isoformat(sep=" ", timespec="seconds"),
                                                   display_relative(switch.timestamp)))
    else:
        embed = error("No switches logged.")
    return embed


async def get_message_contents(client: discord.Client, channel_id: int, message_id: int):
    channel = client.get_channel(channel_id)
    if channel:
        try:
            original_message = await channel.get_message(message_id)
            return original_message.content or None
        except (discord.errors.Forbidden, discord.errors.NotFound):
            pass

    return None


async def message_card(client: discord.Client, message: db.MessageInfo):
    # Get the original sender of the messages
    try:
        original_sender = await client.get_user_info(message.sender)
    except discord.NotFound:
        # Account was since deleted - rare but we're handling it anyway
        original_sender = None

    embed = discord.Embed()
    embed.timestamp = discord.utils.snowflake_time(message.mid)
    embed.colour = discord.Colour.blue()

    if message.system_name:
        system_value = "{} (`{}`)".format(message.system_name, message.system_hid)
    else:
        system_value = "`{}`".format(message.system_hid)
    embed.add_field(name="System", value=system_value)

    embed.add_field(name="Member", value="{} (`{}`)".format(message.name, message.hid))

    if original_sender:
        sender_name = "{}#{}".format(original_sender.name, original_sender.discriminator)
    else:
        sender_name = "(deleted account {})".format(message.sender)

    embed.add_field(name="Sent by", value=sender_name)

    message_content = await get_message_contents(client, message.channel, message.mid)
    embed.description = message_content or "(unknown, message deleted)"

    embed.set_author(name=message.name, icon_url=message.avatar_url or discord.Embed.Empty)
    return embed


def help_footer_embed() -> discord.Embed:
    embed = discord.Embed()
    embed.set_footer(text="By @Ske#6201 | GitHub: https://github.com/xSke/PluralKit/")
    return embed
