from pluralkit.bot.commands import *


async def set_log(ctx: CommandContext):
    if not ctx.message.author.guild_permissions.administrator:
        raise CommandError("You must be a server administrator to use this command.")

    server = ctx.message.guild
    if not server:
        raise CommandError("This command can not be run in a DM.")

    if not ctx.has_next():
        channel_id = None
    else:
        channel = utils.parse_channel_mention(ctx.pop_str(), server=server)
        if not channel:
            raise CommandError("Channel not found.")
        channel_id = channel.id

    await db.update_server(ctx.conn, server.id, logging_channel_id=channel_id)
    await ctx.reply_ok("Updated logging channel." if channel_id else "Cleared logging channel.")
