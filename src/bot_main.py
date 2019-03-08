import asyncio
import sys

try:
    # uvloop doesn't work on Windows, therefore an optional dependency
    import uvloop
    asyncio.set_event_loop_policy(uvloop.EventLoopPolicy())
except ImportError:
    pass

from pluralkit import bot
bot.run(bot.Config.from_file_and_env(sys.argv[1] if len(sys.argv) > 1 else "pluralkit.conf"))