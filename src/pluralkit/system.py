from datetime import datetime

from collections.__init__ import namedtuple
from typing import Optional, List

from pluralkit import db, errors
from pluralkit.member import Member
from pluralkit.switch import Switch
from pluralkit.utils import generate_hid, contains_custom_emoji, validate_avatar_url_or_raise


class System(namedtuple("System", ["id", "hid", "name", "description", "tag", "avatar_url", "created"])):
    id: int
    hid: str
    name: str
    description: str
    tag: str
    avatar_url: str
    created: datetime

    @staticmethod
    async def get_by_account(conn, account_id: str) -> "System":
        return await db.get_system_by_account(conn, account_id)

    @staticmethod
    async def create_system(conn, account_id: str, system_name: Optional[str] = None) -> "System":
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
        if new_description and len(new_description) > 1024:
            raise errors.DescriptionTooLongError()

        await db.update_system_field(conn, self.id, "description", new_description)

    async def set_tag(self, conn, new_tag: Optional[str]):
        if new_tag:
            if len(new_tag) > 32:
                raise errors.TagTooLongError()

            if contains_custom_emoji(new_tag):
                raise errors.CustomEmojiError()

            members_exceeding = await db.get_members_exceeding(conn, system_id=self.id, length=32 - len(new_tag) - 1)
            if len(members_exceeding) > 0:
                raise errors.TagTooLongWithMembersError([member.name for member in members_exceeding])

        await db.update_system_field(conn, self.id, "tag", new_tag)

    async def set_avatar(self, conn, new_avatar_url: Optional[str]):
        if new_avatar_url:
            validate_avatar_url_or_raise(new_avatar_url)

        await db.update_system_field(conn, self.id, "avatar_url", new_avatar_url)

    async def link_account(self, conn, new_account_id: str):
        async with conn.transaction():
            existing_system = await System.get_by_account(conn, new_account_id)

            if existing_system:
                if existing_system.id == self.id:
                    raise errors.AccountInOwnSystemError()

                raise errors.AccountAlreadyLinkedError(existing_system)

            await db.link_account(conn, self.id, new_account_id)

    async def unlink_account(self, conn, account_id: str):
        async with conn.transaction():
            linked_accounts = await db.get_linked_accounts(conn, self.id)
            if len(linked_accounts) == 1:
                raise errors.UnlinkingLastAccountError()

            await db.unlink_account(conn, self.id, account_id)

    async def get_linked_account_ids(self, conn) -> List[int]:
        return await db.get_linked_accounts(conn, self.id)

    async def delete(self, conn):
        await db.remove_system(conn, self.id)

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

    def get_member_name_limit(self) -> int:
        """Returns the maximum length a member's name or nickname is allowed to be. Depends on the system tag."""
        if self.tag:
            return 32 - len(self.tag) - 1
        else:
            return 32

    def to_json(self):
        return {
            "id": self.hid,
            "name": self.name,
            "description": self.description,
            "tag": self.tag,
            "avatar_url": self.avatar_url
        }
