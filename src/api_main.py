import os

import logging

from aiohttp import web

from pluralkit import db, utils

logging.basicConfig(level=logging.INFO, format="[%(asctime)s] [%(name)s] [%(levelname)s] %(message)s")
logger = logging.getLogger("pluralkit.api")


def db_handler(f):
    async def inner(request):
        async with request.app["pool"].acquire() as conn:
            return await f(request, conn)

    return inner


@db_handler
async def get_system(request: web.Request, conn):
    system = await db.get_system_by_hid(conn, request.match_info["id"])

    if not system:
        raise web.HTTPNotFound()

    members = await db.get_all_members(conn, system.id)

    system_json = system.to_json()
    system_json["members"] = [member.to_json() for member in members]
    return web.json_response(system_json)


@db_handler
async def get_member(request: web.Request, conn):
    member = await db.get_member_by_hid(conn, request.match_info["id"])

    if not member:
        raise web.HTTPNotFound()

    return web.json_response(member.to_json())


@db_handler
async def get_switches(request: web.Request, conn):
    system = await db.get_system_by_hid(conn, request.match_info["id"])

    if not system:
        raise web.HTTPNotFound()

    switches = await utils.get_front_history(conn, system.id, 99999)

    data = [{
        "timestamp": stamp.isoformat(),
        "members": [member.hid for member in members]
    } for stamp, members in switches]

    return web.json_response(data)

@db_handler
async def get_message(request: web.Request, conn):
    message = await db.get_message(conn, request.match_info["id"])
    if not message:
        raise web.HTTPNotFound()

    return web.json_response(message.to_json())

@db_handler
async def get_switch(request: web.Request, conn):
    system = await db.get_system_by_hid(conn, request.match_info["id"])

    if not system:
        raise web.HTTPNotFound()

    members, stamp = await utils.get_fronters(conn, system.id)
    if not stamp:
        # No switch has been registered at all
        raise web.HTTPNotFound()

    data = {
        "timestamp": stamp.isoformat(),
        "members": [member.to_json() for member in members]
    }
    return web.json_response(data)

@db_handler
async def get_switch_name(request: web.Request, conn):
    system = await db.get_system_by_hid(conn, request.match_info["id"])

    if not system:
        raise web.HTTPNotFound()

    members, stamp = await utils.get_fronters(conn, system.id)
    return web.Response(text=members[0].name if members else "(nobody)")

@db_handler
async def get_switch_color(request: web.Request, conn):
    system = await db.get_system_by_hid(conn, request.match_info["id"])

    if not system:
        raise web.HTTPNotFound()

    members, stamp = await utils.get_fronters(conn, system.id)
    return web.Response(text=members[0].color if members else "#ffffff")

app = web.Application()
app.add_routes([
    web.get("/systems/{id}", get_system),
    web.get("/systems/{id}/switches", get_switches),
    web.get("/systems/{id}/switch", get_switch),
    web.get("/systems/{id}/switch/name", get_switch_name),
    web.get("/systems/{id}/switch/color", get_switch_color),
    web.get("/members/{id}", get_member),
    web.get("/messages/{id}", get_message)
])


async def run():
    app["pool"] = await db.connect(
        os.environ["DATABASE_USER"],
        os.environ["DATABASE_PASS"],
        os.environ["DATABASE_NAME"],
        os.environ["DATABASE_HOST"],
        int(os.environ["DATABASE_PORT"])
    )
    return app


web.run_app(run())
