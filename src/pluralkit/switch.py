from collections import namedtuple

from typing import List

from pluralkit import db
from pluralkit.member import Member


class Switch(namedtuple("Switch", ["id", "system", "timestamp", "members"])):
    async def fetch_members(self, conn) -> List[Member]:
        return await db.get_members(conn, self.members)
