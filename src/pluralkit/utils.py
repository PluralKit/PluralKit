from datetime import datetime
from typing import List, Tuple

from pluralkit import db, Member


async def get_fronter_ids(conn, system_id) -> (List[int], datetime):
    switches = await db.front_history(conn, system_id=system_id, count=1)
    if not switches:
        return [], None

    if not switches[0]["members"]:
        return [], switches[0]["timestamp"]

    return switches[0]["members"], switches[0]["timestamp"]


async def get_fronters(conn, system_id) -> (List[Member], datetime):
    member_ids, timestamp = await get_fronter_ids(conn, system_id)

    # Collect in dict and then look up as list, to preserve return order
    members = {member.id: member for member in await db.get_members(conn, member_ids)}
    return [members[member_id] for member_id in member_ids], timestamp


async def get_front_history(conn, system_id, count) -> List[Tuple[datetime, List[Member]]]:
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