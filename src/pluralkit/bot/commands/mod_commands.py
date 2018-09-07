from pluralkit.bot.commands import *

logger = logging.getLogger("pluralkit.commands")


async def set_log(ctx: CommandContext):
    if not ctx.message.author.server_permissions.administrator:
        return CommandError("You must be a server administrator to use this command.")

    server = ctx.message.server
    if not ctx.has_next():
        channel_id = None
    else:
        channel = utils.parse_channel_mention(ctx.pop_str(), server=server)
        if not channel:
            return CommandError("Channel not found.")
        channel_id = channel.id

    await db.update_server(ctx.conn, server.id, logging_channel_id=channel_id)
    return CommandSuccess("Updated logging channel." if channel_id else "Cleared logging channel.")
