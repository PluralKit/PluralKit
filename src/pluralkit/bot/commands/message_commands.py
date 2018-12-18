from pluralkit.bot.commands import *


async def message_info(ctx: CommandContext):
    mid_str = ctx.pop_str(CommandError("You must pass a message ID."))

    try:
        mid = int(mid_str)
    except ValueError:
        raise CommandError("You must pass a valid number as a message ID.")

    # Find the message in the DB
    message = await db.get_message(ctx.conn, mid)
    if not message:
        raise CommandError(
            "Message with ID '{}' not found. Are you sure it's a message proxied by PluralKit?".format(mid))

    await ctx.reply(embed=await embeds.message_card(ctx.client, message))
