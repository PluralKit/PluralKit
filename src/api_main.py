import json
import logging
import os

from aiohttp import web, ClientSession

from pluralkit import db, utils
from pluralkit.errors import PluralKitError
from pluralkit.member import Member
from pluralkit.system import System

logging.basicConfig(level=logging.INFO, format="[%(asctime)s] [%(name)s] [%(levelname)s] %(message)s")
logger = logging.getLogger("pluralkit.api")

def require_system(f):
    async def inner(request):
        if "system" not in request:
            raise web.HTTPUnauthorized()
        return await f(request)
    return inner

@web.middleware
async def error_middleware(request, handler):
    try:
        return await handler(request)
    except json.JSONDecodeError:
        raise web.HTTPBadRequest()
    except PluralKitError as e:
        return web.json_response({"error": e.message}, status=400)

@web.middleware
async def db_middleware(request, handler):
    async with request.app["pool"].acquire() as conn:
        request["conn"] = conn
        return await handler(request)

@web.middleware
async def auth_middleware(request, handler):
    token = request.headers.get("X-Token") or request.query.get("token")
    if token:
        system = await System.get_by_token(request["conn"], token)
        if system:
            request["system"] = system
    return await handler(request)

class Handlers:
    @require_system
    async def get_system(request):
        return web.json_response(request["system"].to_json())

    async def get_other_system(request):
        system_id = request.match_info.get("system")
        system = await System.get_by_hid(request["conn"], system_id)
        if not system:
            raise web.HTTPNotFound()
        return web.json_response(system.to_json())

    async def get_system_members(request):
        system_id = request.match_info.get("system")
        system = await System.get_by_hid(request["conn"], system_id)
        if not system:
            raise web.HTTPNotFound()

        members = await system.get_members(request["conn"])
        return web.json_response([m.to_json() for m in members])

    async def get_system_switches(request):
        system_id = request.match_info.get("system")
        system = await System.get_by_hid(request["conn"], system_id)
        if not system:
            raise web.HTTPNotFound()

        switches = await system.get_switches(request["conn"], 9999)

        cache = {}
        async def hid_getter(member_id):
            if not member_id in cache:
                cache[member_id] = await Member.get_member_by_id(request["conn"], member_id)
            return cache[member_id].hid

        return web.json_response([await s.to_json(hid_getter) for s in switches])

    async def get_system_fronters(request):
        system_id = request.match_info.get("system")
        system = await System.get_by_hid(request["conn"], system_id)

        if not system:
            raise web.HTTPNotFound()

        members, stamp = await utils.get_fronters(request["conn"], system.id)
        if not stamp:
            # No switch has been registered at all
            raise web.HTTPNotFound()

        data = {
            "timestamp": stamp.isoformat(),
            "members": [member.to_json() for member in members]
            }
        return web.json_response(data)

    @require_system
    async def patch_system(request):
        req = await request.json()
        if "name" in req:
            await request["system"].set_name(request["conn"], req["name"])
        if "description" in req:
            await request["system"].set_description(request["conn"], req["description"])
        if "tag" in req:
            await request["system"].set_tag(request["conn"], req["tag"])
        if "avatar_url" in req:
            await request["system"].set_avatar(request["conn"], req["name"])
        if "tz" in req:
            await request["system"].set_time_zone(request["conn"], req["tz"])
        return web.json_response((await System.get_by_id(request["conn"], request["system"].id)).to_json())

    async def get_member(request):
        member_id = request.match_info.get("member")
        member = await Member.get_member_by_hid(request["conn"], None, member_id)
        if not member:
            raise web.HTTPNotFound()
        return web.json_response(member.to_json())

    @require_system
    async def post_member(request):
        req = await request.json()
        member = await request["system"].create_member(request["conn"], req["name"])
        return web.json_response(member.to_json())

    @require_system
    async def patch_member(request):
        member_id = request.match_info.get("member")
        member = await Member.get_member_by_hid(request["conn"], None, member_id)
        if not member:
            raise web.HTTPNotFound()
        if member.system != request["system"].id:
            raise web.HTTPUnauthorized()

        req = await request.json()
        if "name" in req:
            await member.set_name(request["conn"], req["name"])
        if "description" in req:
            await member.set_description(request["conn"], req["description"])
        if "avatar_url" in req:
            await member.set_avatar_url(request["conn"], req["avatar_url"])
        if "color" in req:
            await member.set_color(request["conn"], req["color"])
        if "birthday" in req:
            await member.set_birthdate(request["conn"], req["birthday"])
        if "pronouns" in req:
            await member.set_pronouns(request["conn"], req["pronouns"])
        if "prefix" in req or "suffix" in req:
            await member.set_proxy_tags(request["conn"], req.get("prefix", member.prefix), req.get("suffix", member.suffix))
        return web.json_response((await Member.get_member_by_id(request["conn"], member.id)).to_json())

    @require_system
    async def delete_member(request):
        member_id = request.match_info.get("member")
        member = await Member.get_member_by_hid(request["conn"], None, member_id)
        if not member:
            raise web.HTTPNotFound()
        if member.system != request["system"].id:
            raise web.HTTPUnauthorized()

        await member.delete(request["conn"])

    @require_system
    async def post_switch(request):
        req = await request.json()
        if isinstance(req, str):
            req = [req]
        if req is None:
            req = []
        if not isinstance(req, list):
            raise web.HTTPBadRequest()

        members = [await Member.get_member_by_hid(request["conn"], request["system"].id, hid) for hid in req]
        if not all(members):
            raise web.HTTPNotFound(body=json.dumps({"error": "One or more members not found."}))

        switch = await request["system"].add_switch(request["conn"], members)

        hids = {member.id: member.hid for member in members}
        async def hid_getter(mid):
            return hids[mid]

        return web.json_response(await switch.to_json(hid_getter))

    async def discord_oauth(request):
        code = await request.text()
        async with ClientSession() as sess:
            data = {
                'client_id': os.environ["CLIENT_ID"],
                'client_secret': os.environ["CLIENT_SECRET"],
                'grant_type': 'authorization_code',
                'code': code,
                'redirect_uri': os.environ["REDIRECT_URI"],
                'scope': 'identify'
            }
            headers = {
                'Content-Type': 'application/x-www-form-urlencoded'
            }
            res = await sess.post("https://discordapp.com/api/v6/oauth2/token", data=data, headers=headers)
            if res.status != 200:
                raise web.HTTPBadRequest()

            access_token = (await res.json())["access_token"]
            res = await sess.get("https://discordapp.com/api/v6/users/@me", headers={"Authorization": "Bearer " + access_token})
            user_id = int((await res.json())["id"])

            system = await System.get_by_account(request["conn"], user_id)
            if not system:
                raise web.HTTPUnauthorized()
            return web.Response(text=await system.get_token(request["conn"]))
        
    async def get_message(request):
        mid_str = request.match_info.get("message")
        
        try:
            mid = int(mid_str)
        except ValueError:
            raise web.HTTPBadRquest()

        # Find the message in the DB
        message = await db.get_message(request["conn"], mid)
        if not message:
            raise web.HTTPNotFound()
            
        return web.json_response(message.to_json())

async def run():
    app = web.Application(middlewares=[db_middleware, auth_middleware, error_middleware])

    app.add_routes([
        web.get("/s", Handlers.get_system),
        web.post("/s/switches", Handlers.post_switch),
        web.get("/s/{system}", Handlers.get_other_system),
        web.get("/s/{system}/members", Handlers.get_system_members),
        web.get("/s/{system}/switches", Handlers.get_system_switches),
        web.get("/s/{system}/fronters", Handlers.get_system_fronters),
        web.patch("/s", Handlers.patch_system),
        web.get("/m/{member}", Handlers.get_member),
        web.post("/m", Handlers.post_member),
        web.patch("/m/{member}", Handlers.patch_member),
        web.delete("/m/{member}", Handlers.delete_member),
        web.post("/discord_oauth", Handlers.discord_oauth),
        web.get("/message/{message}", Handlers.get_message)
    ])
    app["pool"] = await db.connect(
        os.environ["DATABASE_URI"]
    )
    return app


web.run_app(run())
