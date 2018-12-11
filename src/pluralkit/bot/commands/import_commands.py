import os
from datetime import datetime

from pluralkit.bot.commands import *


def default_tupperware_id():
    if "TUPPERWARE_ID" in os.environ:
        return int(os.environ["TUPPERWARE_ID"])
    return 431544605209788416


async def import_root(ctx: CommandContext):
    # Only one import method rn, so why not default to Tupperware?
    await import_tupperware(ctx)


async def import_tupperware(ctx: CommandContext):
    # Check if there's a Tupperware bot on the server
    # Main instance of TW has that ID, at least
    tupperware_id = default_tupperware_id()
    if ctx.has_next():
        try:
            id_str = ctx.pop_str()
            tupperware_id = int(id_str)
        except ValueError:
            raise CommandError("'{}' is not a valid ID.".format(id_str))

    tupperware_member = ctx.message.guild.get_member(tupperware_id)
    if not tupperware_member:
        raise CommandError(
            """This command only works in a server where the Tupperware bot is also present. 

If you're trying to import from a Tupperware instance other than the main one (which has the ID {}), pass the ID of that instance as a parameter.""".format(
                default_tupperware_id()))

    # Make sure at the bot has send/read permissions here
    channel_permissions = ctx.message.channel.permissions_for(tupperware_member)
    if not (channel_permissions.read_messages and channel_permissions.send_messages):
        # If it doesn't, throw error
        raise CommandError("This command only works in a channel where the Tupperware bot has read/send access.")

    await ctx.reply("Please reply to this message with `tul!list` (or the server equivalent).")

    # Check to make sure the message is sent by Tupperware, and that the Tupperware response actually belongs to the correct user
    def ensure_account(tw_msg):
        if tw_msg.channel.id != ctx.message.channel.id:
            return False

        if tw_msg.author.id != tupperware_member.id:
            return False

        if not tw_msg.embeds:
            return False

        if not tw_msg.embeds[0].title:
            return False

        return tw_msg.embeds[0].title.startswith(
            "{}#{}".format(ctx.message.author.name, ctx.message.author.discriminator))

    tupperware_page_embeds = []

    try:
        tw_msg: discord.Message = await ctx.client.wait_for("message", check=ensure_account, timeout=60.0 * 5)
    except asyncio.TimeoutError:
        raise CommandError("Tupperware import timed out.")

    tupperware_page_embeds.append(tw_msg.embeds[0].to_dict())

    # Handle Tupperware pagination
    def match_pagination():
        pagination_match = re.search(r"\(page (\d+)/(\d+), \d+ total\)", tw_msg.embeds[0].title)
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
            pages_found[new_page] = tw_msg.embeds[0].to_dict()

            # If this isn't the same page as last check, edit the status message
            if new_page != current_page:
                last_found_time = datetime.utcnow()
                await status_msg.edit(
                    content="Multi-page member list found. Please manually scroll through all the pages. Read {}/{} pages.".format(
                        len(pages_found), total_pages))
            current_page = new_page

            # And sleep a bit to prevent spamming the CPU
            await asyncio.sleep(0.25)

            # Make sure it doesn't spin here for too long, time out after 30 seconds since last new page
            if (datetime.utcnow() - last_found_time).seconds > 30:
                raise CommandError("Pagination scan timed out.")

        # Now that we've got all the pages, put them in the embeds list
        # Make sure to erase the original one we put in above too
        tupperware_page_embeds = list([embed for page, embed in sorted(pages_found.items(), key=lambda x: x[0])])

        # Also edit the status message to indicate we're now importing, and it may take a while because there's probably a lot of members
        await status_msg.edit(content="All pages read. Now importing...")

    # Create new (nameless) system if there isn't any registered
    system = await ctx.get_system()
    if system is None:
        system = await System.create_system(ctx.conn, ctx.message.author.id)

    for embed in tupperware_page_embeds:
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
                    member_suffix = brackets[brackets.index("text") + 4:].strip() or None
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
            existing_member = await Member.get_member_by_name(ctx.conn, system.id, name)
            if not existing_member:
                # Or create a new member
                existing_member = await system.create_member(ctx.conn, name)

            # Save the new stuff in the DB
            await existing_member.set_proxy_tags(ctx.conn, member_prefix, member_suffix)
            await existing_member.set_avatar(ctx.conn, member_avatar)
            await existing_member.set_birthdate(ctx.conn, member_birthdate)
            await existing_member.set_description(ctx.conn, member_description)

    await ctx.reply_ok(
        "System information imported. Try using `pk;system` now.\nYou should probably remove your members from Tupperware to avoid double-posting.")
