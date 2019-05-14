import humanize
import re

import random
import string
from datetime import datetime, timezone, timedelta
from typing import List, Tuple, Union
from urllib.parse import urlparse
from urllib.request import urlopen
from urllib.error import HTTPError
import requests


from pluralkit import db
from pluralkit.errors import InvalidAvatarURLError, AvatarHTTPError, InvalidAvatarContentTypeError, AvatarFileSizeTooLargeError


def display_relative(time: Union[datetime, timedelta]) -> str:
    if isinstance(time, datetime):
        time = datetime.utcnow() - time
    return humanize.naturaldelta(time)


async def get_fronter_ids(conn, system_id) -> (List[int], datetime):
    switches = await db.front_history(conn, system_id=system_id, count=1)
    if not switches:
        return [], None

    if not switches[0]["members"]:
        return [], switches[0]["timestamp"]

    return switches[0]["members"], switches[0]["timestamp"]


async def get_fronters(conn, system_id) -> (List["Member"], datetime):
    member_ids, timestamp = await get_fronter_ids(conn, system_id)

    # Collect in dict and then look up as list, to preserve return order
    members = {member.id: member for member in await db.get_members(conn, member_ids)}
    return [members[member_id] for member_id in member_ids], timestamp


async def get_front_history(conn, system_id, count) -> List[Tuple[datetime, List["pluMember"]]]:
    # Get history from DB
    switches = await db.front_history(conn, system_id=system_id, count=count)
    if not switches:
        return []

    # Get all unique IDs referenced
    all_member_ids = {id for switch in switches for id in switch["members"]}

    # And look them up in the database into a dict
    all_members = {member.id: member for member in await db.get_members(conn, list(all_member_ids))}

    # Collect in array and return
    out = []
    for switch in switches:
        timestamp = switch["timestamp"]
        members = [all_members[id] for id in switch["members"]]
        out.append((timestamp, members))
    return out


def generate_hid() -> str:
    return "".join(random.choices(string.ascii_lowercase, k=5))


def contains_custom_emoji(value):
    return bool(re.search("<a?:\w+:\d+>", value))


def validate_avatar_url_or_raise(url):
    u = urlparse(url)
    if not (u.scheme in ["http", "https"] and u.netloc and u.path):
        raise InvalidAvatarURLError()
    response = ''
    response = requests.head(url) # Requests won't output a ton of garbage to console when there's a 404, just one line.
    if (response.status_code() != 200):
        raise AvatarHTTPError(error)
    u = urlopen(url) # get header info
    u.close() # we don't need to read the file
    ContentType = u.info()['content-type']
    ContentType = str.lower(ContentType) # HTTP header feilds are case insensitive so we may get capital letters from sillier web servers
    ContentLength = int(u.info()['content-length'])
    if (ContentType != 'image/jpeg') and (ContentType != 'image/png') and (ContentType != 'image/gif'): # check for valid avatar filetype
        raise InvalidAvatarContentTypeError()
    elif (ContentLength > 1000000):
        raise AvatarFileSizeTooLargeError()

    # TODO: check file type and size of image