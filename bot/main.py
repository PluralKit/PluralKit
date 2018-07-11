import asyncio

from pluralkit import bot

loop = asyncio.get_event_loop()
loop.run_until_complete(bot.run())