import asyncio
import discord
import logging
import re
from typing import Tuple, Optional, Union

from pluralkit import db
from pluralkit.bot import embeds, utils
from pluralkit.errors import PluralKitError
from pluralkit.member import Member
from pluralkit.system import System

logger = logging.getLogger("pluralkit.bot.commands")


def next_arg(arg_string: str) -> Tuple[str, Optional[str]]:
    # A basic quoted-arg parser
    if arg_string.startswith("\""):
        end_quote = arg_string[1:].find("\"") + 1
        if end_quote > 0:
            return arg_string[1:end_quote], arg_string[end_quote + 1:].strip()
        else:
            return arg_string[1:], None

    next_space = arg_string.find(" ")
    if next_space >= 0:
        return arg_string[:next_space].strip(), arg_string[next_space:].strip()
    else:
        return arg_string.strip(), None


class CommandError(Exception):
    def __init__(self, text: str, help: Tuple[str, str] = None):
        self.text = text
        self.help = help

    def format(self):
        return "\u274c " + self.text, embeds.error("", self.help) if self.help else None


class CommandContext:
    def __init__(self, client: discord.Client, message: discord.Message, conn, args: str):
        self.client = client
        self.message = message
        self.conn = conn
        self.args = args

    async def get_system(self) -> Optional[System]:
        return await db.get_system_by_account(self.conn, self.message.author.id)

    async def ensure_system(self) -> System:
        system = await self.get_system()

        if not system:
            raise CommandError("No system registered to this account. Use `pk;system new` to register one.")

        return system

    def has_next(self) -> bool:
        return bool(self.args)

    def pop_str(self, error: CommandError = None) -> Optional[str]:
        if not self.args:
            if error:
                raise error
            return None

        popped, self.args = next_arg(self.args)
        return popped

    async def pop_system(self, error: CommandError = None) -> System:
        name = self.pop_str(error)
        system = await utils.get_system_fuzzy(self.conn, self.client, name)

        if not system:
            raise CommandError("Unable to find system '{}'.".format(name))

        return system

    async def pop_member(self, error: CommandError = None, system_only: bool = True) -> Member:
        name = self.pop_str(error)

        if system_only:
            system = await self.ensure_system()
        else:
            system = await self.get_system()

        member = await utils.get_member_fuzzy(self.conn, system.id if system else None, name, system_only)
        if not member:
            raise CommandError("Unable to find member '{}'{}.".format(name, " in your system" if system_only else ""))

        return member

    def remaining(self):
        return self.args

    async def reply(self, content=None, embed=None):
        return await self.message.channel.send(content=content, embed=embed)

    async def reply_ok(self, content=None, embed=None):
        return await self.reply(content="\u2705 {}".format(content or ""), embed=embed)

    async def reply_warn(self, content=None, embed=None):
        return await self.reply(content="\u26a0 {}".format(content or ""), embed=embed)

    async def confirm_react(self, user: Union[discord.Member, discord.User], message: discord.Message):
        await message.add_reaction("\u2705")  # Checkmark
        await message.add_reaction("\u274c")  # Red X

        try:
            reaction, _ = await self.client.wait_for("reaction_add",
                                                     check=lambda r, u: u.id == user.id and r.emoji in ["\u2705",
                                                                                                        "\u274c"],
                                                     timeout=60.0 * 5)
            return reaction.emoji == "\u2705"
        except asyncio.TimeoutError:
            raise CommandError("Timed out - try again.")

    async def confirm_text(self, user: discord.Member, channel: discord.TextChannel, confirm_text: str, message: str):
        await self.reply(message)

        try:
            message = await self.client.wait_for("message",
                                                 check=lambda m: m.channel.id == channel.id and m.author.id == user.id,
                                                 timeout=60.0 * 5)
            return message.content.lower() == confirm_text.lower()
        except asyncio.TimeoutError:
            raise CommandError("Timed out - try again.")


import pluralkit.bot.commands.api_commands
import pluralkit.bot.commands.import_commands
import pluralkit.bot.commands.member_commands
import pluralkit.bot.commands.message_commands
import pluralkit.bot.commands.misc_commands
import pluralkit.bot.commands.mod_commands
import pluralkit.bot.commands.switch_commands
import pluralkit.bot.commands.system_commands


async def run_command(ctx: CommandContext, func):
    # lol nested try
    try:
        try:
            await func(ctx)
        except PluralKitError as e:
            raise CommandError(e.message, e.help_page)
    except CommandError as e:
        content, embed = e.format()
        await ctx.reply(content=content, embed=embed)



async def command_dispatch(client: discord.Client, message: discord.Message, conn) -> bool:
    prefix = "^(pk(;|!)|<@{}> )".format(client.user.id)
    commands = [
        (r"system (new|register|create|init)", system_commands.new_system),
        (r"system set", system_commands.system_set),
        (r"system (name|rename)", system_commands.system_name),
        (r"system description", system_commands.system_description),
        (r"system avatar", system_commands.system_avatar),
        (r"system tag", system_commands.system_tag),
        (r"system link", system_commands.system_link),
        (r"system unlink", system_commands.system_unlink),
        (r"system fronter", system_commands.system_fronter),
        (r"system fronthistory", system_commands.system_fronthistory),
        (r"system (delete|remove|destroy|erase)", system_commands.system_delete),
        (r"system frontpercent(age)?", system_commands.system_frontpercent),
        (r"system", system_commands.system_info),

        (r"import tupperware", import_commands.import_tupperware),

        (r"member (new|create|add|register)", member_commands.new_member),
        (r"member set", member_commands.member_set),
        (r"member (name|rename)", member_commands.member_name),
        (r"member description", member_commands.member_description),
        (r"member avatar", member_commands.member_avatar),
        (r"member color", member_commands.member_color),
        (r"member (pronouns|pronoun)", member_commands.member_pronouns),
        (r"member (birthday|birthdate)", member_commands.member_birthdate),
        (r"member proxy", member_commands.member_proxy),
        (r"member (delete|remove|destroy|erase)", member_commands.member_delete),
        (r"member", member_commands.member_info),

        (r"message", message_commands.message_info),

        (r"mod log", mod_commands.set_log),

        (r"invite", misc_commands.invite_link),
        (r"export", misc_commands.export),

        (r"help", misc_commands.show_help),

        (r"switch move", switch_commands.switch_move),
        (r"switch out", switch_commands.switch_out),
        (r"switch", switch_commands.switch_member),

        (r"token (refresh|expire|update)", api_commands.refresh_token),
        (r"token", api_commands.get_token)
    ]

    for pattern, func in commands:
        regex = re.compile(prefix + pattern, re.IGNORECASE)

        cmd = message.content
        match = regex.match(cmd)
        if match:
            remaining_string = cmd[match.span()[1]:].strip()

            ctx = CommandContext(
                client=client,
                message=message,
                conn=conn,
                args=remaining_string
            )

            await run_command(ctx, func)
            return True
    return False
