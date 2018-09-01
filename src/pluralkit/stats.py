from aioinflux import InfluxDBClient


class StatCollector:
    async def report_db_query(self, query_name, time, success):
        pass

    async def report_command(self, command_name, execution_time, response_time):
        pass

    async def report_webhook(self, time, success):
        pass

    async def report_periodical_stats(self, conn):
        pass


class NullStatCollector(StatCollector):
    pass


class InfluxStatCollector(StatCollector):
    @staticmethod
    async def connect():
        client = InfluxDBClient(host="influx", db="pluralkit")
        await client.create_database(db="pluralkit")

        return InfluxStatCollector(client)

    def __init__(self, client):
        self.client = client

    async def report_db_query(self, query_name, time, success):
        await self.client.write({
            "measurement": "database_query",
            "tags": {"query": query_name},
            "fields": {"response_time": time, "success": int(success)}
        })

    async def report_command(self, command_name, execution_time, response_time):
        await self.client.write({
            "measurement": "command",
            "tags": {"command": command_name},
            "fields": {"execution_time": execution_time, "response_time": response_time}
        })

    async def report_webhook(self, time, success):
        await self.client.write({
            "measurement": "webhook",
            "fields": {"response_time": time, "success": int(success)}
        })

    async def report_periodical_stats(self, conn):
        from pluralkit import db

        systems = await db.system_count(conn)
        members = await db.member_count(conn)
        messages = await db.message_count(conn)
        accounts = await db.account_count(conn)

        await self.client.write({
            "measurement": "stats",
            "fields": {
                "systems": systems,
                "members": members,
                "messages": messages,
                "accounts": accounts
            }
        })
