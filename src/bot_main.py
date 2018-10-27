import asyncio
import os
import uvloop

asyncio.set_event_loop_policy(uvloop.EventLoopPolicy())

from pluralkit import bot
bot.run()