from pluralkit.bot import help
from pluralkit.bot.commands import *

logger = logging.getLogger("pluralkit.commands")


async def get_message_contents(client: discord.Client, channel_id: int, message_id: int):
    channel = client.get_channel(str(channel_id))
    if channel:
        try:
            original_message = await client.get_channel(channel).get_message(message_id)
            return original_message.content or None
        except (discord.errors.Forbidden, discord.errors.NotFound):
            pass

    return None

async def message_info(ctx: CommandContext):
    mid_str = ctx.pop_str(CommandError("You must pass a message ID.", help=help.message_lookup))

    try:
        mid = int(mid_str)
    except ValueError:
        return CommandError("You must pass a valid number as a message ID.", help=help.message_lookup)

    # Find the message in the DB
    message = await db.get_message(ctx.conn, mid)
    if not message:
        raise CommandError("Message with ID '{}' not found.".format(mid))

    # Get the original sender of the messages
    try:
        original_sender = await ctx.client.get_user_info(message.sender)
    except discord.NotFound:
        # Account was since deleted - rare but we're handling it anyway
        original_sender = None

    embed = discord.Embed()
    embed.timestamp = discord.utils.snowflake_time(mid)
    embed.colour = discord.Colour.blue()

    if message.system_name:
        system_value = "{} (`{}`)".format(message.system_name, message.system_hid)
    else:
        system_value = "`{}`".format(message.system_hid)
    embed.add_field(name="System", value=system_value)

    embed.add_field(name="Member", value="{} (`{}`)".format(message.name, message.hid))

    if original_sender:
        sender_name = "{}#{}".format(original_sender.name, original_sender.discriminator)
    else:
        sender_name = "(deleted account {})".format(message.sender)

    embed.add_field(name="Sent by", value=sender_name)

    message_content = await get_message_contents(ctx.client, message.channel, message.mid)
    embed.description = message_content or "(unknown, message deleted)"

    embed.set_author(name=message.name, icon_url=message.avatar_url or discord.Embed.Empty)

    await ctx.reply(embed=embed)
