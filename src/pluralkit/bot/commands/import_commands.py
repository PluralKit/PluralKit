import asyncio
import re
from datetime import datetime
import logging
from typing import List

from pluralkit.bot import utils
from pluralkit.bot.commands import *

logger = logging.getLogger("pluralkit.commands")

@command(cmd="import tupperware", description="Import data from Tupperware.", system_required=False)
async def import_tupperware(ctx: CommandContext, args: List[str]):
    tupperware_ids = ["431544605209788416", "433916057053560832"]  # Main bot instance and Multi-Pals-specific fork
    tupperware_members = [ctx.message.server.get_member(bot_id) for bot_id in tupperware_ids if ctx.message.server.get_member(bot_id)]

    # Check if there's any Tupperware bot on the server
    if not tupperware_members:
        raise CommandError("This command only works in a server where the Tupperware bot is also present.")

    # Make sure at least one of the bts have send/read permissions here
    for bot_member in tupperware_members:
        channel_permissions = ctx.message.channel.permissions_for(bot_member)
        if channel_permissions.read_messages and channel_permissions.send_messages:
            # If so, break out of the loop
            break
    else:
        # If no bots have permission (ie. loop doesn't break), throw error
        raise CommandError("This command only works in a channel where the Tupperware bot has read/send access.")

    await ctx.reply(embed=utils.make_default_embed("Please reply to this message with `tul!list` (or the server equivalent)."))
    
    # Check to make sure the message is sent by Tupperware, and that the Tupperware response actually belongs to the correct user
    def ensure_account(tw_msg):
        if tw_msg.author not in tupperware_members:
            return False

        if not tw_msg.embeds:
            return False

        if not tw_msg.embeds[0]["title"]:
            return False
        
        return tw_msg.embeds[0]["title"].startswith("{}#{}".format(ctx.message.author.name, ctx.message.author.discriminator))

    embeds = []
    
    tw_msg: discord.Message = await ctx.client.wait_for_message(channel=ctx.message.channel, timeout=60.0, check=ensure_account)
    if not tw_msg:
        raise CommandError("Tupperware import timed out.")
    embeds.append(tw_msg.embeds[0])

    # Handle Tupperware pagination
    def match_pagination():
        pagination_match = re.search(r"\(page (\d+)/(\d+), \d+ total\)", tw_msg.embeds[0]["title"])
        if not pagination_match:
            return None
        return int(pagination_match.group(1)), int(pagination_match.group(2))

    pagination_match = match_pagination()
    if pagination_match:
        status_msg = await ctx.reply("Multi-page member list found. Please manually scroll through all the pages.")
        current_page = 0
        total_pages = 1

        pages_found = {}

        # Keep trying to read the embed with new pages
        last_found_time = datetime.utcnow()
        while len(pages_found) < total_pages:
            new_page, total_pages = match_pagination()

            # Put the found page in the pages dict
            pages_found[new_page] = dict(tw_msg.embeds[0])

            # If this isn't the same page as last check, edit the status message
            if new_page != current_page:
                last_found_time = datetime.utcnow()
                await ctx.client.edit_message(status_msg, "Multi-page member list found. Please manually scroll through all the pages. Read {}/{} pages.".format(len(pages_found), total_pages))
            current_page = new_page

            # And sleep a bit to prevent spamming the CPU
            await asyncio.sleep(0.25)

            # Make sure it doesn't spin here for too long, time out after 30 seconds since last new page
            if (datetime.utcnow() - last_found_time).seconds > 30:
                raise CommandError("Pagination scan timed out.")

        # Now that we've got all the pages, put them in the embeds list
        # Make sure to erase the original one we put in above too
        embeds = list([embed for page, embed in sorted(pages_found.items(), key=lambda x: x[0])])

        # Also edit the status message to indicate we're now importing, and it may take a while because there's probably a lot of members
        await ctx.client.edit_message(status_msg, "All pages read. Now importing...")

    logger.debug("Importing from Tupperware...")

    # Create new (nameless) system if there isn't any registered
    system = ctx.system
    if system is None:
        hid = utils.generate_hid()
        logger.debug("Creating new system (hid={})...".format(hid))
        system = await db.create_system(ctx.conn, system_name=None, system_hid=hid)
        await db.link_account(ctx.conn, system_id=system["id"], account_id=ctx.message.author.id)

    for embed in embeds:
        for field in embed["fields"]:
            name = field["name"]
            lines = field["value"].split("\n")

            member_prefix = None
            member_suffix = None
            member_avatar = None
            member_birthdate = None
            member_description = None

            # Read the message format line by line
            for line in lines:
                if line.startswith("Brackets:"):
                    brackets = line[len("Brackets: "):]
                    member_prefix = brackets[:brackets.index("text")].strip() or None
                    member_suffix = brackets[brackets.index("text")+4:].strip() or None
                elif line.startswith("Avatar URL: "):
                    url = line[len("Avatar URL: "):]
                    member_avatar = url
                elif line.startswith("Birthday: "):
                    bday_str = line[len("Birthday: "):]
                    bday = datetime.strptime(bday_str, "%a %b %d %Y")
                    if bday:
                        member_birthdate = bday.date()
                elif line.startswith("Total messages sent: ") or line.startswith("Tag: "):
                    # Ignore this, just so it doesn't catch as the description
                    pass
                else:
                    member_description = line

            # Read by name - TW doesn't allow name collisions so we're safe here (prevents dupes)
            existing_member = await db.get_member_by_name(ctx.conn, system_id=system.id, member_name=name)
            if not existing_member:
                # Or create a new member
                hid = utils.generate_hid()
                logger.debug("Creating new member {} (hid={})...".format(name, hid))
                existing_member = await db.create_member(ctx.conn, system_id=system.id, member_name=name, member_hid=hid)

            # Save the new stuff in the DB
            logger.debug("Updating fields...")
            await db.update_member_field(ctx.conn, member_id=existing_member.id, field="prefix", value=member_prefix)
            await db.update_member_field(ctx.conn, member_id=existing_member.id, field="suffix", value=member_suffix)
            await db.update_member_field(ctx.conn, member_id=existing_member.id, field="avatar_url", value=member_avatar)
            await db.update_member_field(ctx.conn, member_id=existing_member.id, field="birthday", value=member_birthdate)
            await db.update_member_field(ctx.conn, member_id=existing_member.id, field="description", value=member_description)
    
    return "System information imported. Try using `pk;system` now.\nYou should probably remove your members from Tupperware to avoid double-posting."
