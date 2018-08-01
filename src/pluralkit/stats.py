from aioinflux import InfluxDBClient

client = None
async def connect():
    global client
    client = InfluxDBClient(host="influx", db="pluralkit")
    await client.create_database(db="pluralkit")

async def report_db_query(query_name, time, success):
    if not client:
        return

    await client.write({
        "measurement": "database_query",
        "tags": {"query": query_name},
        "fields": {"response_time": time, "success": int(success)}
    })

async def report_command(command_name, execution_time, response_time):
    if not client:
        return

    await client.write({
        "measurement": "command",
        "tags": {"command": command_name},
        "fields": {"execution_time": execution_time, "response_time": response_time}
    })

async def report_webhook(time, success):
    if not client:
        return

    await client.write({
        "measurement": "webhook",
        "fields": {"response_time": time, "success": int(success)}
    })

async def report_periodical_stats(conn):
    if not client:
        return

    from pluralkit import db

    systems = await db.system_count(conn)
    members = await db.member_count(conn)
    messages = await db.message_count(conn)
    accounts = await db.account_count(conn)

    await client.write({
        "measurement": "stats",
        "fields": {
            "systems": systems,
            "members": members,
            "messages": messages,
            "accounts": accounts
        }
    })