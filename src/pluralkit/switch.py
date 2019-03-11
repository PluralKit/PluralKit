from collections import namedtuple
from datetime import datetime
from typing import List

from pluralkit import db
from pluralkit.member import Member


class Switch(namedtuple("Switch", ["id", "system", "timestamp", "members"])):
    id: int
    system: int
    timestamp: datetime
    members: List[int]

    async def fetch_members(self, conn) -> List[Member]:
        return await db.get_members(conn, self.members)

    async def delete(self, conn):
        await db.delete_switch(conn, self.id)

    async def move(self, conn, new_timestamp):
        await db.move_switch(conn, self.system, self.id, new_timestamp)

    async def to_json(self, hid_getter):
        return {
            "timestamp": self.timestamp.isoformat(),
            "members": [await hid_getter(m) for m in self.members]
        }
