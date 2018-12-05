import re
from datetime import date, datetime

from collections.__init__ import namedtuple
from typing import Optional, Union

from pluralkit import db, errors
from pluralkit.utils import validate_avatar_url_or_raise, contains_custom_emoji


class Member(namedtuple("Member",
                        ["id", "hid", "system", "color", "avatar_url", "name", "birthday", "pronouns", "description",
                         "prefix", "suffix", "created"])):
    """An immutable representation of a system member fetched from the database."""
    id: int
    hid: str
    system: int
    color: str
    avatar_url: str
    name: str
    birthday: date
    pronouns: str
    description: str
    prefix: str
    suffix: str
    created: datetime

    def to_json(self):
        return {
            "id": self.hid,
            "name": self.name,
            "color": self.color,
            "avatar_url": self.avatar_url,
            "birthday": self.birthday.isoformat() if self.birthday else None,
            "pronouns": self.pronouns,
            "description": self.description,
            "prefix": self.prefix,
            "suffix": self.suffix
        }

    @staticmethod
    async def get_member_by_name(conn, system_id: int, member_name: str) -> "Optional[Member]":
        """Fetch a member by the given name in the given system from the database."""
        member = await db.get_member_by_name(conn, system_id, member_name)
        return member

    @staticmethod
    async def get_member_by_hid(conn, system_id: Optional[int], member_hid: str) -> "Optional[Member]":
        """Fetch a member by the given hid from the database. If @`system_id` is present, will only return members from that system."""
        if system_id:
            member = await db.get_member_by_hid_in_system(conn, system_id, member_hid)
        else:
            member = await db.get_member_by_hid(conn, member_hid)

        return member

    async def set_name(self, conn, new_name: str):
        """
        Set the name of a member.
        :raises: CustomEmojiError
        """
        # Custom emojis can't go in the member name
        # Technically they *could*, but they wouldn't render properly
        # so I'd rather explicitly ban them to in order to avoid confusion
        if contains_custom_emoji(new_name):
            raise errors.CustomEmojiError()

        await db.update_member_field(conn, self.id, "name", new_name)

    async def set_description(self, conn, new_description: Optional[str]):
        """
        Set or clear the description of a member.
        :raises: DescriptionTooLongError
        """
        # Explicit length checking
        if new_description and len(new_description) > 1024:
            raise errors.DescriptionTooLongError()

        await db.update_member_field(conn, self.id, "description", new_description)

    async def set_avatar(self, conn, new_avatar_url: Optional[str]):
        """
        Set or clear the avatar of a member.
        :raises: InvalidAvatarURLError
        """
        if new_avatar_url:
            validate_avatar_url_or_raise(new_avatar_url)

        await db.update_member_field(conn, self.id, "avatar_url", new_avatar_url)

    async def set_color(self, conn, new_color: Optional[str]):
        """
        Set or clear the associated color of a member.
        :raises: InvalidColorError
        """
        cleaned_color = None
        if new_color:
            match = re.fullmatch("#?([0-9A-Fa-f]{6})", new_color)
            if not match:
                raise errors.InvalidColorError()

            cleaned_color = match.group(1).lower()

        await db.update_member_field(conn, self.id, "color", cleaned_color)

    async def set_birthdate(self, conn, new_date: Union[date, str]):
        """
        Set or clear the birthdate of a member. To hide the birth year, pass a year of 0001.

        If passed a string, will attempt to parse the string as a date.
        :raises: InvalidDateStringError
        """

        if isinstance(new_date, str):
            date_str = new_date
            try:
                new_date = datetime.strptime(date_str, "%Y-%m-%d").date()
            except ValueError:
                try:
                    # Try again, adding 0001 as a placeholder year
                    # This is considered a "null year" and will be omitted from the info card
                    # Useful if you want your birthday to be displayed yearless.
                    new_date = datetime.strptime("0001-" + date_str, "%Y-%m-%d").date()
                except ValueError:
                    raise errors.InvalidDateStringError()

        await db.update_member_field(conn, self.id, "birthday", new_date)

    async def set_pronouns(self, conn, new_pronouns: str):
        """Set or clear the associated pronouns with a member."""
        await db.update_member_field(conn, self.id, "pronouns", new_pronouns)

    async def set_proxy_tags(self, conn, prefix: Optional[str], suffix: Optional[str]):
        """
        Set the proxy tags for a member. Having no prefix *and* no suffix will disable proxying.
        """
        # Make sure empty strings or other falsey values are actually None
        prefix = prefix or None
        suffix = suffix or None

        async with conn.transaction():
            await db.update_member_field(conn, member_id=self.id, field="prefix", value=prefix)
            await db.update_member_field(conn, member_id=self.id, field="suffix", value=suffix)

    async def delete(self, conn):
        """Delete this member from the database."""
        await db.delete_member(conn, self.id)

    async def fetch_system(self, conn) -> "System":
        """Fetch the member's system from the database"""
        return await db.get_system(conn, self.system)

    async def message_count(self, conn) -> int:
        return await db.get_member_message_count(conn, self.id)