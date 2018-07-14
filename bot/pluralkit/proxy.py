import os
import json
import time

import aiohttp
import discord

from pluralkit import db
from pluralkit.bot import client, logger

def make_log_embed(hook_message, member, channel_name):
    author_name = "#{}: {}".format(channel_name, member["name"])
    if member["system_name"]:
        author_name += " ({})".format(member["system_name"])

    embed = discord.Embed()
    embed.colour = discord.Colour.blue()
    embed.description = hook_message.clean_content
    embed.timestamp = hook_message.timestamp
    embed.set_author(name=author_name, icon_url=member["avatar_url"] or discord.Embed.Empty)
    
    if len(hook_message.attachments) > 0:
        embed.set_image(url=hook_message.attachments[0]["url"])
    return embed

async def log_message(original_message, hook_message, member, log_channel):
    # hook_message is kinda broken, and doesn't include details from server or channel
    # We rely on the fact that original_message must be in the same channel, this'll break if that changes
    embed = make_log_embed(hook_message, member, channel_name=original_message.channel.name)
    embed.set_footer(text="System ID: {} | Member ID: {} | Sender: {}#{} | Message ID: {}".format(member["system_hid"], member["hid"], original_message.author.name, original_message.author.discriminator, hook_message.id))

    message_link = "https://discordapp.com/channels/{}/{}/{}".format(original_message.server.id, original_message.channel.id, hook_message.id)
    embed.author.url = message_link

    await client.send_message(log_channel, embed=embed)

async def log_delete(hook_message, member, log_channel):
    embed = make_log_embed(hook_message, member, channel_name=hook_message.channel.name)
    embed.set_footer(text="System ID: {} | Member ID: {} | Message ID: {}".format(member["system_hid"], member["hid"], hook_message.id))
    embed.colour = discord.Colour.dark_red()

    await client.send_message(log_channel, embed=embed)

async def get_log_channel(conn, server):
    # Check server info for a log channel
    server_info = await db.get_server_info(conn, server.id)
    if server_info and server_info["log_channel"]:
        channel = server.get_channel(str(server_info["log_channel"]))
        return channel

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
        if member["avatar_url"]:
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
                msg = discord.Message(reactions=[], **resp_data)

                # Make sure it's added to the client's message cache - otherwise events r
                #client.messages.append(msg)
                return msg
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
    await db.add_message(conn, message_id=hook_message.id, channel_id=trigger_message.channel.id, member_id=member["id"], sender_id=trigger_message.author.id, content=inner)

    # Log message to logging channel if necessary
    log_channel = await get_log_channel(conn, trigger_message.server)
    if log_channel:
        await log_message(trigger_message, hook_message, member, log_channel)

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
    msg_clean = message.clean_content
    for member in members:
        # If no proxy details are configured, skip
        if not member["prefix"] and not member["suffix"]:
            continue

        # Database stores empty strings as null, fix that here
        prefix = member["prefix"] or ""
        suffix = member["suffix"] or ""

        # If we have a match, proxy the message
        # Match on the cleaned message to prevent a prefix of "<" catching on a mention
        if msg_clean.startswith(prefix) and msg_clean.endswith(suffix):
            # Extract the actual message contents sans tags
            if suffix:
                inner_message = msg[len(prefix):-len(suffix)].strip()
            else:
                # Slicing to -0 breaks, don't do that
                inner_message = msg[len(prefix):].strip()

            # Make sure the message isn't blank
            if inner_message:
                await proxy_message(conn, member, message, inner_message)
            break


async def handle_reaction(conn, user_id, message_id, emoji):
    if emoji == "❌":
        async with conn.transaction():
            # Find the message in the DB, and make sure it's sent by the user who reacted
            db_message = await db.get_message_by_sender_and_id(conn, message_id=message_id, sender_id=user_id)
            if db_message:
                logger.debug("Deleting message {} by reaction from {}".format(message_id, user_id))
                
                # If so, remove it from the DB
                await db.delete_message(conn, message_id)

                # And look up the message and then delete it
                channel = client.get_channel(str(db_message["channel"]))
                message = await client.get_message(channel, message_id)
                await client.delete_message(message)

                # Log deletion to logging channel if necessary
                log_channel = await get_log_channel(conn, message.server)
                if log_channel:
                    # db_message contains enough member data for the things to work
                    await log_delete(message, db_message, log_channel)