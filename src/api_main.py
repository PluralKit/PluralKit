import json
import logging
import os

from aiohttp import web

from pluralkit import db, utils
from pluralkit.errors import PluralKitError
from pluralkit.member import Member
from pluralkit.system import System

logging.basicConfig(level=logging.INFO, format="[%(asctime)s] [%(name)s] [%(levelname)s] %(message)s")
logger = logging.getLogger("pluralkit.api")


def db_handler(f):
    async def inner(request, *args, **kwargs):
        async with request.app["pool"].acquire() as conn:
            return await f(request, conn=conn, *args, **kwargs)

    return inner


def system_auth(f):
    async def inner(request: web.Request, conn, *args, **kwargs):
        token = request.headers.get("X-Token")
        if not token:
            token = request.query.get("token")

        if not token:
            raise web.HTTPUnauthorized()

        system = await System.get_by_token(conn, token)
        if not system:
            raise web.HTTPUnauthorized()

        return await f(request, conn=conn, system=system, *args, **kwargs)

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


@db_handler
@system_auth
async def put_switch(request: web.Request, system: System, conn):
    try:
        req = await request.json()
    except json.JSONDecodeError:
        raise web.HTTPBadRequest(body="Invalid JSON")

    if isinstance(req, str):
        req = [req]
    elif not isinstance(req, list):
        raise web.HTTPBadRequest(body="Body must be JSON string or list")

    members = []
    for member_name in req:
        if not isinstance(member_name, str):
            raise web.HTTPBadRequest(body="List value must be string")

        member = await Member.get_member_fuzzy(conn, system.id, member_name)
        if not member:
            raise web.HTTPBadRequest(body="Member '{}' not found".format(member_name))
        members.append(member)

    switch = await system.add_switch(conn, members)
    return web.json_response(await switch.to_json(conn))


@db_handler
async def get_stats(request: web.Request, conn):
    system_count = await db.system_count(conn)
    member_count = await db.member_count(conn)
    message_count = await db.message_count(conn)

    return web.json_response({
        "systems": system_count,
        "members": member_count,
        "messages": message_count
    })


@web.middleware
async def render_pk_errors(request, handler):
    try:
        return await handler(request)
    except PluralKitError as e:
        raise web.HTTPBadRequest(body=e.message)


app = web.Application(middlewares=[render_pk_errors])
app.add_routes([
    web.get("/systems/{id}", get_system),
    web.get("/systems/{id}/switches", get_switches),
    web.get("/systems/{id}/switch", get_switch),
    web.put("/systems/{id}/switch", put_switch),
    web.get("/systems/{id}/switch/name", get_switch_name),
    web.get("/systems/{id}/switch/color", get_switch_color),
    web.get("/members/{id}", get_member),
    web.get("/messages/{id}", get_message),
    web.get("/stats", get_stats)
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
