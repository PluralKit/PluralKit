from io import BytesIO

import discord
import logging
import re
from typing import List, Optional

from pluralkit import db
from pluralkit.bot import utils, channel_logger
from pluralkit.bot.channel_logger import ChannelLogger

logger = logging.getLogger("pluralkit.bot.proxy")


def extract_leading_mentions(message_text):
    # This regex matches one or more mentions at the start of a message, separated by any amount of spaces
    match = re.match(r"^(<(@|@!|#|@&|a?:\w+:)\d+>\s*)+", message_text)
    if not match:
        return message_text, ""

    # Return the text after the mentions, and the mentions themselves
    return message_text[match.span(0)[1]:].strip(), match.group(0)


def match_member_proxy_tags(member: db.ProxyMember, message_text: str):
    # Skip members with no defined proxy tags
    if not member.prefix and not member.suffix:
        return None

    # DB defines empty prefix/suffixes as None, replace with empty strings to prevent errors
    prefix = member.prefix or ""
    suffix = member.suffix or ""

    # Ignore mentions at the very start of the message, and match proxy tags after those
    message_text, leading_mentions = extract_leading_mentions(message_text)

    logger.debug(
        "Matching text '{}' and leading mentions '{}' to proxy tags {}text{}".format(message_text, leading_mentions,
                                                                                     prefix, suffix))

    if message_text.startswith(member.prefix or "") and message_text.endswith(member.suffix or ""):
        prefix_length = len(prefix)
        suffix_length = len(suffix)

        # If suffix_length is 0, the last bit of the slice will be "-0", and the slice will fail
        if suffix_length > 0:
            inner_string = message_text[prefix_length:-suffix_length]
        else:
            inner_string = message_text[prefix_length:]

        # Add the mentions we stripped back
        inner_string = leading_mentions + inner_string
        return inner_string


def match_proxy_tags(members: List[db.ProxyMember], message_text: str):
    # Sort by specificity (members with both prefix and suffix go higher)
    # This will make sure more "precise" proxy tags get tried first
    members: List[db.ProxyMember] = sorted(members, key=lambda x: int(
        bool(x.prefix)) + int(bool(x.suffix)), reverse=True)

    for member in members:
        match = match_member_proxy_tags(member, message_text)
        if match is not None:  # Using "is not None" because an empty string is OK here too
            logger.debug("Matched member {} with inner text '{}'".format(member.hid, match))
            return member, match


async def get_or_create_webhook_for_channel(conn, channel: discord.TextChannel):
    # First, check if we have one saved in the DB
    webhook_from_db = await db.get_webhook(conn, channel.id)
    if webhook_from_db:
        webhook_id, webhook_token = webhook_from_db

        session = channel._state.http._session
        hook = discord.Webhook.partial(webhook_id, webhook_token, adapter=discord.AsyncWebhookAdapter(session))

        # Workaround for https://github.com/Rapptz/discord.py/issues/1242
        hook._adapter.store_user = hook._adapter._store_user
        return hook

    # If not, we create one and save it
    created_webhook = await channel.create_webhook(name="PluralKit Proxy Webhook")
    await db.add_webhook(conn, channel.id, created_webhook.id, created_webhook.token)
    return created_webhook


async def make_attachment_file(message: discord.Message):
    if not message.attachments:
        return None

    first_attachment = message.attachments[0]

    # Copy the file data to the buffer
    # TODO: do this without buffering... somehow
    bio = BytesIO()
    await first_attachment.save(bio)

    return discord.File(bio, first_attachment.filename)


async def do_proxy_message(conn, original_message: discord.Message, proxy_member: db.ProxyMember,
                           inner_text: str, logger: ChannelLogger):
    # Send the message through the webhook
    webhook = await get_or_create_webhook_for_channel(conn, original_message.channel)

    try:
        sent_message = await webhook.send(
            content=inner_text,
            username=proxy_member.name,
            avatar_url=proxy_member.avatar_url,
            file=await make_attachment_file(original_message),
            wait=True
        )
    except discord.NotFound:
        # The webhook we got from the DB doesn't actually exist
        # If we delete it from the DB then call the function again, it'll re-create one for us
        await db.delete_webhook(conn, original_message.channel.id)
        await do_proxy_message(conn, original_message, proxy_member, inner_text, logger)
        return

    # Save the proxied message in the database
    await db.add_message(conn, sent_message.id, original_message.channel.id, proxy_member.id,
                         original_message.author.id)

    await logger.log_message_proxied(
        conn,
        original_message.channel.guild.id,
        original_message.channel.name,
        original_message.channel.id,
        original_message.author.name,
        original_message.author.discriminator,
        original_message.author.id,
        proxy_member.name,
        proxy_member.hid,
        proxy_member.avatar_url,
        proxy_member.system_name,
        proxy_member.system_hid,
        inner_text,
        sent_message.attachments[0].url if sent_message.attachments else None,
        sent_message.created_at,
        sent_message.id
    )

    # And finally, gotta delete the original.
    await original_message.delete()


async def try_proxy_message(conn, message: discord.Message, logger: ChannelLogger) -> bool:
    # Don't bother proxying in DMs with the bot
    if isinstance(message.channel, discord.abc.PrivateChannel):
        return False

    # Return every member associated with the account
    members = await db.get_members_by_account(conn, message.author.id)
    proxy_match = match_proxy_tags(members, message.content)
    if not proxy_match:
        # No proxy tags match here, we done
        return False

    member, inner_text = proxy_match

    # Sanitize inner text for @everyones and such
    inner_text = utils.sanitize(inner_text)

    # If we don't have an inner text OR an attachment, we cancel because the hook can't send that
    if not inner_text and not message.attachments:
        return False

    # So, we now have enough information to successfully proxy a message
    async with conn.transaction():
        await do_proxy_message(conn, message, member, inner_text, logger)
    return True


async def handle_deleted_message(conn, client: discord.Client, message_id: int,
                                 message_content: Optional[str], logger: channel_logger.ChannelLogger) -> bool:
    msg = await db.get_message(conn, message_id)
    if not msg:
        return False

    channel = client.get_channel(msg.channel)
    if not channel:
        # Weird edge case, but channel *could* be deleted at this point (can't think of any scenarios it would be tho)
        return False

    await db.delete_message(conn, message_id)
    await logger.log_message_deleted(
        conn,
        channel.guild.id,
        channel.name,
        msg.name,
        msg.hid,
        msg.avatar_url,
        msg.system_name,
        msg.system_hid,
        message_content,
        message_id
    )
    return True


async def try_delete_by_reaction(conn, client: discord.Client, message_id: int, reaction_user: int,
                                 logger: channel_logger.ChannelLogger) -> bool:
    # Find the message by the given message id or reaction user
    msg = await db.get_message_by_sender_and_id(conn, message_id, reaction_user)
    if not msg:
        # Either the wrong user reacted or the message isn't a proxy message
        # In either case - not our problem
        return False

    # Find the original message
    original_message = await client.get_channel(msg.channel).get_message(message_id)
    if not original_message:
        # Message got deleted, possibly race condition, eh
        return False

    # Then delete the original message
    await original_message.delete()

    await handle_deleted_message(conn, client, message_id, original_message.content, logger)