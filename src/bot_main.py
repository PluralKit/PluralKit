import asyncio
import os
import uvloop

asyncio.set_event_loop_policy(uvloop.EventLoopPolicy())

from pluralkit import bot

pk = bot.PluralKitBot(os.environ["TOKEN"])
loop = asyncio.get_event_loop()
loop.run_until_complete(pk.run())