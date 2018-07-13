import os
import json
import time

import aiohttp
import discord

from pluralkit import db
from pluralkit.bot import client, logger

async def log_message(original_message, hook_message, member, log_channel):
    author_name = "#{}: {}".format(original_message.channel.name, member["name"])
    if member["system_name"]:
        author_name += " ({})".format(member["system_name"])

    embed = discord.Embed()
    embed.colour = discord.Colour.blue()
    embed.description = hook_message.clean_content
    embed.timestamp = hook_message.timestamp
    embed.set_author(name=author_name, icon_url=member["avatar_url"] or discord.Embed.Empty)
    embed.set_footer(text="System ID: {} | Member ID: {} | Sender: {}#{} | Message ID: {}".format(member["system_hid"], member["hid"], original_message.author.name, original_message.author.discriminator, hook_message.id))
    
    if len(hook_message.attachments) > 0:
        embed.set_image(url=hook_message.attachments[0]["url"])

    await client.send_message(log_channel, embed=embed)

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

async def send_hook_message(member, hook_id, hook_token, text=None, image_url=None):
    async with aiohttp.ClientSession() as session:
        # Set up headers
        req_headers = {
            "Authorization": "Bot {}".format(os.environ["TOKEN"])
        }

        # Set up parameters
        # Use FormData because the API doesn't like JSON requests with file data
        fd = aiohttp.FormData()
        fd.add_field("username", "{} {}".format(member["name"], member["tag"] or "").strip())
        fd.add_field("avatar_url", member["avatar_url"])

        if text:
            fd.add_field("content", text)

        if image_url:
            # Fetch the image URL and proxy it directly into the file data (async streaming!)
            image_resp = await session.get(image_url)
            fd.add_field("file", image_resp.data, content_type=image_resp.content_type, filename=image_resp.url.name)

        # Send the actual webhook request, and wait for a response
        async with session.post("https://discordapp.com/api/v6/webhooks/{}/{}?wait=true".format(hook_id, hook_token),
            data=fd,
            headers=req_headers) as resp:
            if resp.status == 200:
                resp_data = await resp.json()
                # Make a fake message object for passing on - this is slightly broken but works for most things
                return discord.Message(reactions=[], **resp_data)
            else:
                # Fake a Discord exception, also because #yolo
                raise discord.HTTPException(resp, await resp.text())


async def proxy_message(conn, member, trigger_message, inner):
    logger.debug("Proxying message '{}' for member {}".format(inner, member["hid"]))

    # Get the webhook details
    hook_id, hook_token = await get_webhook(conn, trigger_message.channel)

    # Get attachment image URL if present (only works for one...)
    image_urls = [a["url"] for a in trigger_message.attachments if "url" in a]
    image_url = image_urls[0] if len(image_urls) > 0 else None

    # Send the hook message
    hook_message = await send_hook_message(member, hook_id, hook_token, text=inner, image_url=image_url)

    # Insert new message details into the DB
    await db.add_message(conn, message_id=hook_message.id, channel_id=trigger_message.channel.id, member_id=member["id"], sender_id=trigger_message.author.id)

    # Check server info for a log channel
    server_info = await db.get_server_info(conn, trigger_message.server.id)
    if server_info and server_info["log_channel"]:
        channel = trigger_message.server.get_channel(str(server_info["log_channel"]))
        if channel:
            # Log the message to the log channel if present
            await log_message(trigger_message, hook_message, member, channel)

    # Delete the original message
    await client.delete_message(trigger_message)


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
