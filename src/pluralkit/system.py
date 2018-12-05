import random
import re
import string
from datetime import datetime

from collections.__init__ import namedtuple
from typing import Optional, List, Tuple

from pluralkit import db, errors
from pluralkit.member import Member
from pluralkit.switch import Switch
from pluralkit.utils import generate_hid, contains_custom_emoji, validate_avatar_url_or_raise


class System(namedtuple("System", ["id", "hid", "name", "description", "tag", "avatar_url", "token", "created"])):
    id: int
    hid: str
    name: str
    description: str
    tag: str
    avatar_url: str
    token: str
    created: datetime

    @staticmethod
    async def get_by_account(conn, account_id: int) -> Optional["System"]:
        return await db.get_system_by_account(conn, account_id)

    @staticmethod
    async def get_by_token(conn, token: str) -> Optional["System"]:
        return await db.get_system_by_token(conn, token)

    @staticmethod
    async def create_system(conn, account_id: int, system_name: Optional[str] = None) -> "System":
        async with conn.transaction():
            existing_system = await System.get_by_account(conn, account_id)
            if existing_system:
                raise errors.ExistingSystemError()

            new_hid = generate_hid()

            async with conn.transaction():
                new_system = await db.create_system(conn, system_name, new_hid)
                await db.link_account(conn, new_system.id, account_id)

            return new_system

    async def set_name(self, conn, new_name: Optional[str]):
        await db.update_system_field(conn, self.id, "name", new_name)

    async def set_description(self, conn, new_description: Optional[str]):
        # Explicit length error
        if new_description and len(new_description) > 1024:
            raise errors.DescriptionTooLongError()

        await db.update_system_field(conn, self.id, "description", new_description)

    async def set_tag(self, conn, new_tag: Optional[str]):
        if new_tag:
            # Explicit length error
            if len(new_tag) > 32:
                raise errors.TagTooLongError()

            if contains_custom_emoji(new_tag):
                raise errors.CustomEmojiError()

        await db.update_system_field(conn, self.id, "tag", new_tag)

    async def set_avatar(self, conn, new_avatar_url: Optional[str]):
        if new_avatar_url:
            validate_avatar_url_or_raise(new_avatar_url)

        await db.update_system_field(conn, self.id, "avatar_url", new_avatar_url)

    async def link_account(self, conn, new_account_id: int):
        async with conn.transaction():
            existing_system = await System.get_by_account(conn, new_account_id)

            if existing_system:
                if existing_system.id == self.id:
                    raise errors.AccountInOwnSystemError()

                raise errors.AccountAlreadyLinkedError(existing_system)

            await db.link_account(conn, self.id, new_account_id)

    async def unlink_account(self, conn, account_id: int):
        async with conn.transaction():
            linked_accounts = await db.get_linked_accounts(conn, self.id)
            if len(linked_accounts) == 1:
                raise errors.UnlinkingLastAccountError()

            await db.unlink_account(conn, self.id, account_id)

    async def get_linked_account_ids(self, conn) -> List[int]:
        return await db.get_linked_accounts(conn, self.id)

    async def delete(self, conn):
        await db.remove_system(conn, self.id)

    async def refresh_token(self, conn) -> str:
        new_token = "".join(random.choices(string.ascii_letters + string.digits, k=64))
        await db.update_system_field(conn, self.id, "token", new_token)
        return new_token

    async def create_member(self, conn, member_name: str) -> Member:
        # TODO: figure out what to do if this errors out on collision on generate_hid
        new_hid = generate_hid()

        if len(member_name) > self.get_member_name_limit():
            raise errors.MemberNameTooLongError(tag_present=bool(self.tag))

        member = await db.create_member(conn, self.id, member_name, new_hid)
        return member

    async def get_members(self, conn) -> List[Member]:
        return await db.get_all_members(conn, self.id)

    async def get_switches(self, conn, count) -> List[Switch]:
        """Returns the latest `count` switches logged for this system, ordered latest to earliest."""
        return [Switch(**s) for s in await db.front_history(conn, self.id, count)]

    async def get_latest_switch(self, conn) -> Optional[Switch]:
        """Returns the latest switch logged for this system, or None if no switches have been logged"""
        switches = await self.get_switches(conn, 1)
        if switches:
            return switches[0]
        else:
            return None

    async def add_switch(self, conn, members: List[Member]):
        async with conn.transaction():
            switch_id = await db.add_switch(conn, self.id)

            # TODO: batch query here
            for member in members:
                await db.add_switch_member(conn, switch_id, member.id)

    def get_member_name_limit(self) -> int:
        """Returns the maximum length a member's name or nickname is allowed to be in order for the member to be proxied. Depends on the system tag."""
        if self.tag:
            return 32 - len(self.tag) - 1
        else:
            return 32

    async def match_proxy(self, conn, message: str) -> Optional[Tuple[Member, str]]:
        """Tries to find a member with proxy tags matching the given message. Returns the member and the inner contents."""
        members = await db.get_all_members(conn, self.id)

        # Sort by specificity (members with both prefix and suffix defined go higher)
        # This will make sure more "precise" proxy tags get tried first and match properly
        members = sorted(members, key=lambda x: int(bool(x.prefix)) + int(bool(x.suffix)), reverse=True)

        for member in members:
            proxy_prefix = member.prefix or ""
            proxy_suffix = member.suffix or ""

            if not proxy_prefix and not proxy_suffix:
                # If the member has neither a prefix or a suffix, cancel early
                # Otherwise it'd match any message no matter what
                continue

            # Check if the message matches these tags
            if message.startswith(proxy_prefix) and message.endswith(proxy_suffix):
                # If the message starts with a mention, "separate" that and match the bit after
                mention_match = re.match(r"^(<(@|@!|#|@&|a?:\w+:)\d+>\s*)+", message)
                leading_mentions = ""
                if mention_match:
                    message = message[mention_match.span(0)[1]:].strip()
                    leading_mentions = mention_match.group(0)

                # Extract the inner message (special case because -0 is invalid as an end slice)
                if len(proxy_suffix) == 0:
                    inner_message = message[len(proxy_prefix):]
                else:
                    inner_message = message[len(proxy_prefix):-len(proxy_suffix)]

                # Add the stripped mentions back if there are any
                inner_message = leading_mentions + inner_message
                return member, inner_message

    def to_json(self):
        return {
            "id": self.hid,
            "name": self.name,
            "description": self.description,
            "tag": self.tag,
            "avatar_url": self.avatar_url
        }
