import logging
from collections import namedtuple

import asyncpg
import discord

import pluralkit
from pluralkit import db
from pluralkit.bot import utils

logger = logging.getLogger("pluralkit.bot.commands")

command_list = {}

class InvalidCommandSyntax(Exception):
    pass

class NoSystemRegistered(Exception):
    pass
    
class CommandError(Exception):
    def __init__(self, message):
        self.message = message

class CommandContext(namedtuple("CommandContext", ["client", "conn", "message", "system"])):
    client: discord.Client
    conn: asyncpg.Connection
    message: discord.Message
    system: pluralkit.System

    async def reply(self, message=None, embed=None):
        return await self.client.send_message(self.message.channel, message, embed=embed)

class MemberCommandContext(namedtuple("MemberCommandContext", CommandContext._fields + ("member",)), CommandContext):
    client: discord.Client
    conn: asyncpg.Connection
    message: discord.Message
    system: pluralkit.System
    member: pluralkit.Member

class CommandEntry(namedtuple("CommandEntry", ["command", "function", "usage", "description", "category"])):
    pass

def command(cmd, usage=None, description=None, category=None, system_required=True):
    def wrap(func):
        async def wrapper(client, conn, message, args):
            system = await db.get_system_by_account(conn, message.author.id)

            if system_required and system is None:
                await client.send_message(message.channel, embed=utils.make_error_embed("No system registered to this account. Use `pk;system new` to register one."))
                return
            
            ctx = CommandContext(client=client, conn=conn, message=message, system=system)
            try:
                res = await func(ctx, args)

                if res:
                    embed = res if isinstance(res, discord.Embed) else utils.make_default_embed(res)
                    await client.send_message(message.channel, embed=embed)
            except NoSystemRegistered:
                await client.send_message(message.channel, embed=utils.make_error_embed("No system registered to this account. Use `pk;system new` to register one."))
            except InvalidCommandSyntax:
                usage_str = "**Usage:** pk;{} {}".format(cmd, usage or "")
                await client.send_message(message.channel, embed=utils.make_default_embed(usage_str))
            except CommandError as e:
                embed = e.message if isinstance(e.message, discord.Embed) else utils.make_error_embed(e.message)
                await client.send_message(message.channel, embed=embed)
            except Exception:
                logger.exception("Exception while handling command {} (args={}, system={})".format(cmd, args, system.hid if system else "(none)"))

        # Put command in map
        command_list[cmd] = CommandEntry(command=cmd, function=wrapper, usage=usage, description=description, category=category)
        return wrapper
    return wrap

def member_command(cmd, usage=None, description=None, category=None, system_only=True):
    def wrap(func):
        async def wrapper(ctx: CommandContext, args):
            # Return if no member param
            if len(args) == 0:
                raise InvalidCommandSyntax()

            # System is allowed to be none if not system_only
            system_id = ctx.system.id if ctx.system else None
            # And find member by key
            member = await utils.get_member_fuzzy(ctx.conn, system_id=system_id, key=args[0], system_only=system_only)

            if member is None:
                raise CommandError("Can't find member \"{}\".".format(args[0]))

            ctx = MemberCommandContext(client=ctx.client, conn=ctx.conn, message=ctx.message, system=ctx.system, member=member)
            return await func(ctx, args[1:])
        return command(cmd=cmd, usage="<name|id> {}".format(usage or ""), description=description, category=category, system_required=False)(wrapper)
    return wrap

import pluralkit.bot.commands.import_commands
import pluralkit.bot.commands.member_commands
import pluralkit.bot.commands.message_commands
import pluralkit.bot.commands.misc_commands
import pluralkit.bot.commands.mod_commands
import pluralkit.bot.commands.switch_commands
import pluralkit.bot.commands.system_commands
