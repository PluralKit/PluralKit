from collections import namedtuple
from datetime import datetime
import logging
from typing import List, Optional
import time

import asyncpg
import asyncpg.exceptions
from discord.utils import snowflake_time

from pluralkit.system import System
from pluralkit.member import Member

logger = logging.getLogger("pluralkit.db")
async def connect(uri):
    while True:
        try:
            return await asyncpg.create_pool(uri)
        except (ConnectionError, asyncpg.exceptions.CannotConnectNowError):
            logger.exception("Failed to connect to database, retrying in 5 seconds...")
            time.sleep(5)

def db_wrap(func):
    async def inner(*args, **kwargs):
        before = time.perf_counter()
        try:
            res = await func(*args, **kwargs)
            after = time.perf_counter()

            logger.debug(" - DB call {} took {:.2f} ms".format(func.__name__, (after - before) * 1000))
            return res
        except asyncpg.exceptions.PostgresError:
            logger.exception("Error from database query {}".format(func.__name__))
    return inner

@db_wrap
async def create_system(conn, system_name: str, system_hid: str) -> System:
    logger.debug("Creating system (name={}, hid={})".format(
        system_name, system_hid))
    row = await conn.fetchrow("insert into systems (name, hid) values ($1, $2) returning *", system_name, system_hid)
    return System(**row) if row else None


@db_wrap
async def remove_system(conn, system_id: int):
    logger.debug("Deleting system (id={})".format(system_id))
    await conn.execute("delete from systems where id = $1", system_id)


@db_wrap
async def create_member(conn, system_id: int, member_name: str, member_hid: str) -> Member:
    logger.debug("Creating member (system={}, name={}, hid={})".format(
        system_id, member_name, member_hid))
    row = await conn.fetchrow("insert into members (name, system, hid) values ($1, $2, $3) returning *", member_name, system_id, member_hid)
    return Member(**row) if row else None


@db_wrap
async def delete_member(conn, member_id: int):
    logger.debug("Deleting member (id={})".format(member_id))
    await conn.execute("delete from members where id = $1", member_id)


@db_wrap
async def link_account(conn, system_id: int, account_id: int):
    logger.debug("Linking account (account_id={}, system_id={})".format(
        account_id, system_id))
    await conn.execute("insert into accounts (uid, system) values ($1, $2)", account_id, system_id)


@db_wrap
async def unlink_account(conn, system_id: int, account_id: int):
    logger.debug("Unlinking account (account_id={}, system_id={})".format(
        account_id, system_id))
    await conn.execute("delete from accounts where uid = $1 and system = $2", account_id, system_id)


@db_wrap
async def get_linked_accounts(conn, system_id: int) -> List[int]:
    return [row["uid"] for row in await conn.fetch("select uid from accounts where system = $1", system_id)]


@db_wrap
async def get_system_by_account(conn, account_id: int) -> System:
    row = await conn.fetchrow("select systems.* from systems, accounts where accounts.uid = $1 and accounts.system = systems.id", account_id)
    return System(**row) if row else None

@db_wrap
async def get_system_by_token(conn, token: str) -> Optional[System]:
    row = await conn.fetchrow("select * from systems where token = $1", token)
    return System(**row) if row else None

@db_wrap
async def get_system_by_hid(conn, system_hid: str) -> System:
    row = await conn.fetchrow("select * from systems where hid = $1", system_hid)
    return System(**row) if row else None


@db_wrap
async def get_system(conn, system_id: int) -> System:
    row = await conn.fetchrow("select * from systems where id = $1", system_id)
    return System(**row) if row else None


@db_wrap
async def get_member_by_name(conn, system_id: int, member_name: str) -> Member:
    row = await conn.fetchrow("select * from members where system = $1 and lower(name) = lower($2)", system_id, member_name)
    return Member(**row) if row else None


@db_wrap
async def get_member_by_hid_in_system(conn, system_id: int, member_hid: str) -> Member:
    row = await conn.fetchrow("select * from members where system = $1 and hid = $2", system_id, member_hid)
    return Member(**row) if row else None


@db_wrap
async def get_member_by_hid(conn, member_hid: str) -> Member:
    row = await conn.fetchrow("select * from members where hid = $1", member_hid)
    return Member(**row) if row else None


@db_wrap
async def get_member(conn, member_id: int) -> Member:
    row = await conn.fetchrow("select * from members where id = $1", member_id)
    return Member(**row) if row else None

@db_wrap
async def get_members(conn, members: list) -> List[Member]:
    rows = await conn.fetch("select * from members where id = any($1)", members)
    return [Member(**row) for row in rows]

@db_wrap
async def update_system_field(conn, system_id: int, field: str, value):
    logger.debug("Updating system field (id={}, {}={})".format(
        system_id, field, value))
    await conn.execute("update systems set {} = $1 where id = $2".format(field), value, system_id)


@db_wrap
async def update_member_field(conn, member_id: int, field: str, value):
    logger.debug("Updating member field (id={}, {}={})".format(
        member_id, field, value))
    await conn.execute("update members set {} = $1 where id = $2".format(field), value, member_id)


@db_wrap
async def get_all_members(conn, system_id: int) -> List[Member]:
    rows = await conn.fetch("select * from members where system = $1", system_id)
    return [Member(**row) for row in rows]

@db_wrap
async def get_members_exceeding(conn, system_id: int, length: int) -> List[Member]:
    rows = await conn.fetch("select * from members where system = $1 and length(name) > $2", system_id, length)
    return [Member(**row) for row in rows]


@db_wrap
async def get_webhook(conn, channel_id: int) -> (str, str):
    row = await conn.fetchrow("select webhook, token from webhooks where channel = $1", channel_id)
    return (str(row["webhook"]), row["token"]) if row else None


@db_wrap
async def add_webhook(conn, channel_id: int, webhook_id: int, webhook_token: str):
    logger.debug("Adding new webhook (channel={}, webhook={}, token={})".format(
        channel_id, webhook_id, webhook_token))
    await conn.execute("insert into webhooks (channel, webhook, token) values ($1, $2, $3)", channel_id, webhook_id, webhook_token)

@db_wrap
async def delete_webhook(conn, channel_id: int):
    await conn.execute("delete from webhooks where channel = $1", channel_id)

@db_wrap
async def add_message(conn, message_id: int, channel_id: int, member_id: int, sender_id: int):
    logger.debug("Adding new message (id={}, channel={}, member={}, sender={})".format(
        message_id, channel_id, member_id, sender_id))
    await conn.execute("insert into messages (mid, channel, member, sender) values ($1, $2, $3, $4)", message_id, channel_id, member_id, sender_id)

class ProxyMember(namedtuple("ProxyMember", ["id", "hid", "prefix", "suffix", "color", "name", "avatar_url", "tag", "system_name", "system_hid"])):
    id: int
    hid: str
    prefix: str
    suffix: str
    color: str
    name: str
    avatar_url: str
    tag: str
    system_name: str
    system_hid: str

@db_wrap
async def get_members_by_account(conn, account_id: int) -> List[ProxyMember]:
    # Returns a "chimera" object
    rows = await conn.fetch("""select
            members.id, members.hid, members.prefix, members.suffix, members.color, members.name, members.avatar_url,
            systems.tag, systems.name as system_name, systems.hid as system_hid
        from
            systems, members, accounts
        where
            accounts.uid = $1
            and systems.id = accounts.system
            and members.system = systems.id""", account_id)
    return [ProxyMember(**row) for row in rows]

class MessageInfo(namedtuple("MemberInfo", ["mid", "channel", "member", "sender", "name", "hid", "avatar_url", "system_name", "system_hid"])):
    mid: int
    channel: int
    member: int
    sender: int
    name: str
    hid: str
    avatar_url: str
    system_name: str
    system_hid: str

    def to_json(self):
        return {
            "id": str(self.mid),
            "channel": str(self.channel),
            "member": self.hid,
            "system": self.system_hid,
            "message_sender": str(self.sender),
            "timestamp": snowflake_time(self.mid).isoformat()
        }

@db_wrap
async def get_message_by_sender_and_id(conn, message_id: int, sender_id: int) -> MessageInfo:
    row = await conn.fetchrow("""select
        messages.*,
        members.name, members.hid, members.avatar_url,
        systems.name as system_name, systems.hid as system_hid
    from
        messages, members, systems
    where
        messages.member = members.id
        and members.system = systems.id
        and mid = $1
        and sender = $2""", message_id, sender_id)
    return MessageInfo(**row) if row else None


@db_wrap
async def get_message(conn, message_id: int) -> MessageInfo:
    row = await conn.fetchrow("""select
        messages.*,
        members.name, members.hid, members.avatar_url,
        systems.name as system_name, systems.hid as system_hid
    from
        messages, members, systems
    where
        messages.member = members.id
        and members.system = systems.id
        and mid = $1""", message_id)
    return MessageInfo(**row) if row else None


@db_wrap
async def delete_message(conn, message_id: int):
    logger.debug("Deleting message (id={})".format(message_id))
    await conn.execute("delete from messages where mid = $1", message_id)

@db_wrap
async def get_member_message_count(conn, member_id: int) -> int:
    return await conn.fetchval("select count(*) from messages where member = $1", member_id)

@db_wrap
async def front_history(conn, system_id: int, count: int):
    return await conn.fetch("""select
        switches.*,
        array(
            select member from switch_members
            where switch_members.switch = switches.id
            order by switch_members.id asc
        ) as members
    from switches
    where switches.system = $1
    order by switches.timestamp desc
    limit $2""", system_id, count)

@db_wrap
async def add_switch(conn, system_id: int):
    logger.debug("Adding switch (system={})".format(system_id))
    res = await conn.fetchrow("insert into switches (system) values ($1) returning *", system_id)
    return res["id"]

@db_wrap
async def move_switch(conn, system_id: int, switch_id: int, new_time: datetime):
    logger.debug("Moving latest switch (system={}, id={}, new_time={})".format(system_id, switch_id, new_time))
    await conn.execute("update switches set timestamp = $1 where system = $2 and id = $3", new_time, system_id, switch_id)

@db_wrap
async def add_switch_member(conn, switch_id: int, member_id: int):
    logger.debug("Adding switch member (switch={}, member={})".format(switch_id, member_id))
    await conn.execute("insert into switch_members (switch, member) values ($1, $2)", switch_id, member_id)

@db_wrap
async def delete_switch(conn, switch_id: int):
    logger.debug("Deleting switch (id={})".format(switch_id))
    await conn.execute("delete from switches where id = $1", switch_id)

@db_wrap
async def get_server_info(conn, server_id: int):
    return await conn.fetchrow("select * from servers where id = $1", server_id)

@db_wrap
async def update_server(conn, server_id: int, logging_channel_id: int):
    logging_channel_id = logging_channel_id if logging_channel_id else None
    logger.debug("Updating server settings (id={}, log_channel={})".format(server_id, logging_channel_id))
    await conn.execute("insert into servers (id, log_channel) values ($1, $2) on conflict (id) do update set log_channel = $2", server_id, logging_channel_id)

@db_wrap
async def member_count(conn) -> int:
    return await conn.fetchval("select count(*) from members")

@db_wrap
async def system_count(conn) -> int:
    return await conn.fetchval("select count(*) from systems")

@db_wrap
async def message_count(conn) -> int:
    return await conn.fetchval("select count(*) from messages")

@db_wrap
async def account_count(conn) -> int:
    return await conn.fetchval("select count(*) from accounts")

async def create_tables(conn):
    await conn.execute("""create table if not exists systems (
        id          serial primary key,
        hid         char(5) unique not null,
        name        text,
        description text,
        tag         text,
        avatar_url  text,
        token       text,
        created     timestamp not null default (current_timestamp at time zone 'utc'),
        ui_tz       text not null default 'UTC'
    )""")
    await conn.execute("""create table if not exists members (
        id          serial primary key,
        hid         char(5) unique not null,
        system      serial not null references systems(id) on delete cascade,
        color       char(6),
        avatar_url  text,
        name        text not null,
        birthday    date,
        pronouns    text,
        description text,
        prefix      text,
        suffix      text,
        created     timestamp not null default (current_timestamp at time zone 'utc')
    )""")
    await conn.execute("""create table if not exists accounts (
        uid         bigint primary key,
        system      serial not null references systems(id) on delete cascade
    )""")
    await conn.execute("""create table if not exists messages (
        mid         bigint primary key,
        channel     bigint not null,
        member      serial not null references members(id) on delete cascade,
        sender      bigint not null
    )""")
    await conn.execute("""create table if not exists switches (
        id          serial primary key,
        system      serial not null references systems(id) on delete cascade,
        timestamp   timestamp not null default (current_timestamp at time zone 'utc')
    )""")
    await conn.execute("""create table if not exists switch_members (
        id          serial primary key,
        switch      serial not null references switches(id) on delete cascade,
        member      serial not null references members(id) on delete cascade
    )""")
    await conn.execute("""create table if not exists webhooks (
        channel     bigint primary key,
        webhook     bigint not null,
        token       text not null
    )""")
    await conn.execute("""create table if not exists servers (
        id          bigint primary key,
        log_channel bigint
    )""")
