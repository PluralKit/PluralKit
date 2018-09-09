from datetime import datetime

from collections.__init__ import namedtuple


class System(namedtuple("System", ["id", "hid", "name", "description", "tag", "avatar_url", "created"])):
    id: int
    hid: str
    name: str
    description: str
    tag: str
    avatar_url: str
    created: datetime

    def to_json(self):
        return {
            "id": self.hid,
            "name": self.name,
            "description": self.description,
            "tag": self.tag,
            "avatar_url": self.avatar_url
        }