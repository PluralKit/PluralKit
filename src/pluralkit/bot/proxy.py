import asyncio
import re

import discord
from io import BytesIO
from typing import Optional

from pluralkit import db
from pluralkit.bot import utils, channel_logger
from pluralkit.bot.channel_logger import ChannelLogger
from pluralkit.member import Member
from pluralkit.system import System


class ProxyError(Exception):
    pass

async def get_or_create_webhook_for_channel(conn, bot_user: discord.User, channel: discord.TextChannel):
    # First, check if we have one saved in the DB
    webhook_from_db = await db.get_webhook(conn, channel.id)
    if webhook_from_db:
        webhook_id, webhook_token = webhook_from_db

        session = channel._state.http._session
        hook = discord.Webhook.partial(webhook_id, webhook_token, adapter=discord.AsyncWebhookAdapter(session))
        return hook

    try:
        # If not, we check to see if there already exists one we've missed
        for existing_hook in await channel.webhooks():
            existing_hook_creator = existing_hook.user.id if existing_hook.user else None
            is_mine = existing_hook.name == "PluralKit Proxy Webhook" and existing_hook_creator == bot_user.id
            if is_mine:
                # We found one we made, let's add that to the DB just to be sure
                await db.add_webhook(conn, channel.id, existing_hook.id, existing_hook.token)
                return existing_hook

        # If not, we create one and save it
        created_webhook = await channel.create_webhook(name="PluralKit Proxy Webhook")
    except discord.Forbidden:
        raise ProxyError(
            "PluralKit does not have the \"Manage Webhooks\" permission, and thus cannot proxy your message. Please contact a server administrator.")

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


def fix_clyde(name: str) -> str:
    # Discord doesn't allow any webhook username to contain the word "Clyde"
    # So replace "Clyde" with "C lyde" (except with a hair space, hence \u200A)
    # Zero-width spacers are ignored by Discord and will still trigger the error
    return re.sub("(c)(lyde)", "\\1\u200A\\2", name, flags=re.IGNORECASE)


async def send_proxy_message(conn, original_message: discord.Message, system: System, member: Member,
                             inner_text: str, logger: ChannelLogger, bot_user: discord.User):
    # Send the message through the webhook
    webhook = await get_or_create_webhook_for_channel(conn, bot_user, original_message.channel)

    # Bounds check the combined name to avoid silent erroring
    full_username = "{} {}".format(member.name, system.tag or "").strip()
    full_username = fix_clyde(full_username)
    if len(full_username) < 2:
        raise ProxyError(
            "The webhook's name, `{}`, is shorter than two characters, and thus cannot be proxied. Please change the member name or use a longer system tag.".format(
                full_username))
    if len(full_username) > 32:
        raise ProxyError(
            "The webhook's name, `{}`, is longer than 32 characters, and thus cannot be proxied. Please change the member name or use a shorter system tag.".format(
                full_username))

    try:
        sent_message = await webhook.send(
            content=inner_text,
            username=full_username,
            avatar_url=member.avatar_url,
            file=await make_attachment_file(original_message),
            wait=True
        )
    except discord.NotFound:
        # The webhook we got from the DB doesn't actually exist
        # This can happen if someone manually deletes it from the server
        # If we delete it from the DB then call the function again, it'll re-create one for us
        # (lol, lazy)
        await db.delete_webhook(conn, original_message.channel.id)
        await send_proxy_message(conn, original_message, system, member, inner_text, logger, bot_user)
        return

    # Save the proxied message in the database
    await db.add_message(conn, sent_message.id, original_message.channel.id, member.id,
                         original_message.author.id)

    # Log it in the log channel if possible
    await logger.log_message_proxied(
        conn,
        original_message.channel.guild.id,
        original_message.channel.name,
        original_message.channel.id,
        original_message.author.name,
        original_message.author.discriminator,
        original_message.author.id,
        member.name,
        member.hid,
        member.avatar_url,
        system.name,
        system.hid,
        inner_text,
        sent_message.attachments[0].url if sent_message.attachments else None,
        sent_message.created_at,
        sent_message.id
    )

    # And finally, gotta delete the original.
    # We wait half a second or so because if the client receives the message deletion
    # event before the message actually gets confirmed sent on their end, the message
    # doesn't properly get deleted for them, leading to duplication
    try:
        await asyncio.sleep(0.5)
        await original_message.delete()
    except discord.Forbidden:
        raise ProxyError(
            "PluralKit does not have permission to delete user messages. Please contact a server administrator.")
    except discord.NotFound:
        # Sometimes some other thing will delete the original message before PK gets to it
        # This is not a problem - message gets deleted anyway :)
        # Usually happens when Tupperware and PK conflict
        pass


async def try_proxy_message(conn, message: discord.Message, logger: ChannelLogger, bot_user: discord.User) -> bool:
    # Don't bother proxying in DMs
    if isinstance(message.channel, discord.abc.PrivateChannel):
        return False

    # Get the system associated with the account, if possible
    system = await System.get_by_account(conn, message.author.id)
    if not system:
        return False

    # Match on the members' proxy tags
    proxy_match = await system.match_proxy(conn, message.content)
    if not proxy_match:
        return False

    member, inner_message = proxy_match

    # Make sure no @everyones slip through
    # Webhooks implicitly have permission to mention @everyone so we have to enforce that manually
    inner_message = utils.sanitize(inner_message)

    # If we don't have an inner text OR an attachment, we cancel because the hook can't send that
    # Strip so it counts a string of solely spaces as blank too
    if not inner_message.strip() and not message.attachments:
        return False

    # So, we now have enough information to successfully proxy a message
    async with conn.transaction():
        try:
            await send_proxy_message(conn, message, system, member, inner_message, logger, bot_user)
        except ProxyError as e:
            # First, try to send the error in the channel it was triggered in
            # Failing that, send the error in a DM.
            # Failing *that*... give up, I guess.
            try:
                await message.channel.send("\u274c {}".format(str(e)))
            except discord.Forbidden:
                try:
                    await message.author.send("\u274c {}".format(str(e)))
                except discord.Forbidden:
                    pass

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
