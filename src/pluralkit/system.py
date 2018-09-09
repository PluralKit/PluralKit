from datetime import datetime

from collections.__init__ import namedtuple
from typing import Optional

from pluralkit import db, errors
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

        await db.update_system_field(conn, self.id, "avatar", new_avatar_url)

    async def link_account(self, conn, new_account_id: str):
        existing_system = await System.get_by_account(conn, new_account_id)
        if existing_system:
            raise errors.AccountAlreadyLinkedError(existing_system)

        await db.link_account(conn, self.id, new_account_id)

    async def unlink_account(self, conn, account_id: str):
        linked_accounts = await db.get_linked_accounts(conn, self.id)
        if len(linked_accounts) == 1:
            raise errors.UnlinkingLastAccountError()

        await db.unlink_account(conn, self.id, account_id)

    async def delete(self, conn):
        await db.remove_system(conn, self.id)

    def to_json(self):
        return {
            "id": self.hid,
            "name": self.name,
            "description": self.description,
            "tag": self.tag,
            "avatar_url": self.avatar_url
        }
