import random
import re
import string
from collections.__init__ import namedtuple
from datetime import datetime
from typing import Optional, List, Tuple

import pytz

from pluralkit import db, errors
from pluralkit.member import Member
from pluralkit.switch import Switch
from pluralkit.utils import generate_hid, contains_custom_emoji, validate_avatar_url_or_raise

class TupperboxImportResult(namedtuple("TupperboxImportResult", ["updated", "created", "tags"])):
    pass

class System(namedtuple("System", ["id", "hid", "name", "description", "tag", "avatar_url", "token", "created", "ui_tz"])):
    id: int
    hid: str
    name: str
    description: str
    tag: str
    avatar_url: str
    token: str
    created: datetime
    # pytz-compatible time zone name, usually Olson-style (eg. Europe/Amsterdam)
    ui_tz: str

    @staticmethod
    async def get_by_id(conn, system_id: int) -> Optional["System"]:
        return await db.get_system(conn, system_id)

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

    async def add_switch(self, conn, members: List[Member]) -> Switch:
        """
        Logs a new switch for a system.

        :raises: MembersAlreadyFrontingError, DuplicateSwitchMembersError
        """
        new_ids = [member.id for member in members]

        last_switch = await self.get_latest_switch(conn)

        # If we have a switch logged before, make sure this isn't a dupe switch
        if last_switch:
            last_switch_members = await last_switch.fetch_members(conn)
            last_ids = [member.id for member in last_switch_members]

            # We don't compare by set() here because swapping multiple is a valid operation
            if last_ids == new_ids:
                raise errors.MembersAlreadyFrontingError(members)

        # Check for dupes
        if len(set(new_ids)) != len(new_ids):
            raise errors.DuplicateSwitchMembersError()

        async with conn.transaction():
            switch_id = await db.add_switch(conn, self.id)

            # TODO: batch query here
            for member in members:
                await db.add_switch_member(conn, switch_id, member.id)

            return await self.get_latest_switch(conn)

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

    def format_time(self, dt: datetime) -> str:
        """
        Localizes the given `datetime` to a string based on the system's preferred time zone.

        Assumes `dt` is a naïve `datetime` instance set to UTC, which is consistent with the rest of PluralKit.
        """
        tz = pytz.timezone(self.ui_tz)

        # Set to aware (UTC), convert to tz, set to naive (tz), then format and append name
        return tz.normalize(pytz.utc.localize(dt)).replace(tzinfo=None).isoformat(sep=" ", timespec="seconds") + " " + tz.tzname(dt)

    async def set_time_zone(self, conn, tz_name: str) -> pytz.tzinfo:
        """
        Sets the system time zone to the time zone represented by the given string.

        If `tz_name` is None or an empty string, will default to UTC.
        If `tz_name` does not represent a valid time zone string, will raise InvalidTimeZoneError.

        :raises: InvalidTimeZoneError
        :returns: The `pytz.tzinfo` instance of the newly set time zone.
        """

        tz = pytz.timezone(tz_name or "UTC")
        await db.update_system_field(conn, self.id, "ui_tz", tz.zone)
        return tz

    async def import_from_tupperbox(self, conn, data: dict):
        """
        Imports from a Tupperbox JSON data file.
        :raises: TupperboxImportError
        """
        if not "tuppers" in data:
            raise errors.TupperboxImportError()
        if not isinstance(data["tuppers"], list):
            raise errors.TupperboxImportError()
        
        all_tags = set()
        created_members = set()
        updated_members = set()
        for tupper in data["tuppers"]:
            # Sanity check tupper fields
            for field in ["name", "avatar_url", "brackets", "birthday", "description", "tag"]:
                if field not in tupper:
                    raise errors.TupperboxImportError()
            
            # Find member by name, create if not exists
            member_name = str(tupper["name"])
            member = await Member.get_member_by_name(conn, self.id, member_name)
            if not member:
                # And keep track of created members
                created_members.add(member_name)
                member = await self.create_member(conn, member_name)
            else:
                # Keep track of updated members
                updated_members.add(member_name)

            # Set avatar
            await member.set_avatar(conn, str(tupper["avatar_url"]))

            # Set proxy tags
            if not (isinstance(tupper["brackets"], list) and len(tupper["brackets"]) >= 2):
                raise errors.TupperboxImportError()
            await member.set_proxy_tags(conn, str(tupper["brackets"][0]), str(tupper["brackets"][1]))

            # Set birthdate (input is in ISO-8601, first 10 characters is the date)
            if tupper["birthday"]:
                try:
                    await member.set_birthdate(conn, str(tupper["birthday"][:10]))
                except errors.InvalidDateStringError:
                    pass
            
            # Set description
            await member.set_description(conn, tupper["description"])

            # Keep track of tag
            all_tags.add(tupper["tag"])

        # Since Tupperbox does tags on a per-member basis, we only apply a system tag if
        # every member has the same tag (surprisingly common)
        # If not, we just do nothing. (This will be reported in the caller function through the returned result)
        if len(all_tags) == 1:
            tag = list(all_tags)[0]
            await self.set_tag(conn, tag)

        return TupperboxImportResult(updated=updated_members, created=created_members, tags=all_tags)
                
    def to_json(self):
        return {
            "id": self.hid,
            "name": self.name,
            "description": self.description,
            "tag": self.tag,
            "avatar_url": self.avatar_url
        }
