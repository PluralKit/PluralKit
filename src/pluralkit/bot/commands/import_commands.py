import aiohttp
import asyncio
import io
import json
import os
from datetime import datetime

from pluralkit.errors import TupperboxImportError
from pluralkit.bot.commands import *

async def import_root(ctx: CommandContext):
    # Only one import method rn, so why not default to Tupperbox?
    await import_tupperbox(ctx)


async def import_tupperbox(ctx: CommandContext):
    await ctx.reply("To import from Tupperbox, reply to this message with a `tuppers.json` file imported from Tupperbox.\n\nTo obtain such a file, type `tul!export` (or your server's equivalent).")
        
    def predicate(msg):
        if msg.author.id != ctx.message.author.id:
            return False
        if msg.attachments:
            if msg.attachments[0].filename.endswith(".json"):
                return True
        return False

    try:
        message = await ctx.client.wait_for("message", check=predicate, timeout=60*5)
    except asyncio.TimeoutError:
        raise CommandError("Timed out. Try running `pk;import` again.")

    s = io.BytesIO()
    await message.attachments[0].save(s)
    data = json.load(s)
    
    system = await ctx.get_system()
    if not system:
        system = await System.create_system(ctx.conn, account_id=ctx.message.author.id)
    
    result = await system.import_from_tupperbox(ctx.conn, data)
    tag_note = ""
    if len(result.tags) > 1:
        tag_note = "\n\nPluralKit's tags work on a per-system basis. Since your Tupperbox members have more than one unique tag, PluralKit has not imported the tags. Set your system tag manually with `pk;system tag <tag>`."
    
    await ctx.reply_ok("Updated {} member{}, created {} member{}. Type `pk;system` to check!{}".format(
        len(result.updated), "s" if len(result.updated) != 1 else "",
        len(result.created), "s" if len(result.created) != 1 else "",
        tag_note
    ))