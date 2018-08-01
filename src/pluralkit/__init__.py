from collections import namedtuple
from datetime import date, datetime


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

class Member(namedtuple("Member", ["id", "hid", "system", "color", "avatar_url", "name", "birthday", "pronouns", "description", "prefix", "suffix", "created"])):
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