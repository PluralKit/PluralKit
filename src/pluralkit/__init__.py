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