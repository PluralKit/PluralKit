from pluralkit.bot.commands import CommandContext

disclaimer = "\u26A0 Please note that this grants access to modify (and delete!) all your system data, so keep it safe and secure. If it leaks or you need a new one, you can invalidate this one with `pk;token refresh`."


async def token_root(ctx: CommandContext):
    if ctx.match("refresh") or ctx.match("expire") or ctx.match("invalidate") or ctx.match("update"):
        await token_refresh(ctx)
    else:
        await token_get(ctx)


async def token_get(ctx: CommandContext):
    system = await ctx.ensure_system()

    if system.token:
        token = system.token
    else:
        token = await system.refresh_token(ctx.conn)

    token_message = "{}\n\u2705 Here's your API token:".format(disclaimer)
    if token:
        await ctx.reply_ok("DM'd!")
        await ctx.message.author.send(token_message)
        await ctx.message.author.send(token)
    return

async def token_refresh(ctx: CommandContext):
    system = await ctx.ensure_system()

    token = await system.refresh_token(ctx.conn)
    token_message = "Your previous API token has been invalidated. You will need to change it anywhere it's currently used.\n{}\n\u2705 Here's your new API token:".format(disclaimer)
    if token:
        await ctx.message.author.send(token_message)
        await ctx.message.author.send(token)
