# PluralKit
PluralKit is a Discord bot meant for plural communities. It has features like message proxying through webhooks, switch tracking, system and member profiles, and more.

**Do you just want to add PluralKit to your server? If so, you don't need any of this. Use the bot's invite link: https://discord.com/oauth2/authorize?client_id=466378653216014359&scope=bot%20applications.commands&permissions=536995904**

PluralKit has a Discord server for support, feedback, and discussion: https://discord.gg/PczBt78 

# Running
In production, we run PluralKit using Kubernetes (soon). The configuration can be found in the infra repo.

For self-hosting, it's simpler to use Docker, with the provided [docker-compose](./docker-compose.yml) file.

Create a `.env` file with the Discord client ID and bot token:
```
CLIENT_ID=198622483471925248
BOT_TOKEN=MTk4NjIyNDgzNDcxOTI1MjQ4.Cl2FMQ.ZnCjm1XVW7vRze4b7Cq4se7kKWs
```

If you want to use `pk;admin` commands (to raise member limits  and such), set `ADMIN_ROLE` to a Discord role ID:

```
ADMIN_ROLE=682632767057428509
```

Run `docker compose build`, then `docker compose up -d`.

To view logs, use `docker compose logs`.

Postgres data is stored in a `pluralkit_data` [Docker volume](https://docs.docker.com/engine/storage/volumes/).

# Development
See [the dev-docs/ directory](./dev-docs/README.md)

# User documentation
See [the docs/ directory](./docs/README.md)

# License
This project is under the GNU Affero General Public License, Version 3. It is available at the following link: https://www.gnu.org/licenses/agpl-3.0.en.html
