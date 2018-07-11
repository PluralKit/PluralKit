import os
import time

import aiohttp

from pluralkit import db
from pluralkit.bot import client, logger


async def get_webhook(conn, channel):
    async with conn.transaction():
        # Try to find an existing webhook
        hook_row = await db.get_webhook(conn, channel_id=channel.id)
        # There's none, we'll make one
        if not hook_row:
            async with aiohttp.ClientSession() as session:
                req_data = {"name": "PluralKit Proxy Webhook"}
                req_headers = {
                    "Authorization": "Bot {}".format(os.environ["TOKEN"])}

                async with session.post("https://discordapp.com/api/v6/channels/{}/webhooks".format(channel.id), json=req_data, headers=req_headers) as resp:
                    data = await resp.json()
                    hook_id = data["id"]
                    token = data["token"]

                    # Insert new hook into DB
                    await db.add_webhook(conn, channel_id=channel.id, webhook_id=hook_id, webhook_token=token)
                    return hook_id, token

        return hook_row["webhook"], hook_row["token"]


async def proxy_message(conn, member, message, inner):
    logger.debug("Proxying message '{}' for member {}".format(
        inner, member["hid"]))
    # Delete the original message
    await client.delete_message(message)

    # Get the webhook details
    hook_id, hook_token = await get_webhook(conn, message.channel)
    async with aiohttp.ClientSession() as session:
        req_data = {
            "username": "{} {}".format(member["name"], member["tag"] or "").strip(),
            "avatar_url": member["avatar_url"],
            "content": inner
        }
        req_headers = {"Authorization": "Bot {}".format(os.environ["TOKEN"])}
        # And send the message
        async with session.post("https://discordapp.com/api/v6/webhooks/{}/{}?wait=true".format(hook_id, hook_token), json=req_data, headers=req_headers) as resp:
            resp_data = await resp.json()
            logger.debug("Discord webhook response: {}".format(resp_data))

            # Insert new message details into the DB
            await db.add_message(conn, message_id=resp_data["id"], channel_id=message.channel.id, member_id=member["id"], sender_id=message.author.id)


async def handle_proxying(conn, message):
    # Big fat query to find every member associated with this account
    # Returned member object has a few more keys (system tag, for example)
    members = await db.get_members_by_account(conn, account_id=message.author.id)

    # Sort by specificity (members with both prefix and suffix go higher)
    members = sorted(members, key=lambda x: int(
        bool(x["prefix"])) + int(bool(x["suffix"])), reverse=True)

    msg = message.content
    for member in members:
        # If no proxy details are configured, skip
        if not member["prefix"] and not member["suffix"]:
            continue

        # Database stores empty strings as null, fix that here
        prefix = member["prefix"] or ""
        suffix = member["suffix"] or ""

        # If we have a match, proxy the message
        if msg.startswith(prefix) and msg.endswith(suffix):
            # Extract the actual message contents sans tags
            if suffix:
                inner_message = message.content[len(
                    prefix):-len(suffix)].strip()
            else:
                # Slicing to -0 breaks, don't do that
                inner_message = message.content[len(prefix):].strip()

            await proxy_message(conn, member, message, inner_message)
            break


async def handle_reaction(conn, reaction, user):
    if reaction.emoji == "❌":
        async with conn.transaction():
            # Find the message in the DB, and make sure it's sent by the user who reacted
            message = await db.get_message_by_sender_and_id(conn, message_id=reaction.message.id, sender_id=user.id)

            if message:
                # If so, delete the message and remove it from the DB
                await db.delete_message(conn, message["mid"])
                await client.delete_message(reaction.message)
