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

    # Check if we can send messages in the log channel. If not, raise an error
    # in the channel. If we don't have permission to do that, DM the user who sent
    # the command and skip updating the log channel in the database
    try:
        # Send a message in the log channel to confirm and also to test permission.
        if channel_id:
            await channel.send("✅ PluralKit will now log proxied messages in this channel.")
        await db.update_server(ctx.conn, server.id, logging_channel_id=channel_id)
        await ctx.reply_ok("Updated logging channel." if channel_id else "Cleared logging channel.")
    except discord.Forbidden:
        # Try (heh) to let the user know we don't have permission to post there
        error_msg = "PluralKit doesn't have permission to read and/or send messages in that channel! Please check my permissions then try again."
        raise CommandError(error_msg)
        # nested try asdhfakjd
        try:
            # Failing that, we DM the user who issued the command
            await ctx.message.author.send(await CommandError(error_msg))
        except discord.Forbidden:
            # and if that fails, well... crap, nothing we can do
            pass