from common import *
from commands import COMMAND_LIST

import io
import os
import sys
import json

from pathlib import Path
from urllib import request
from urllib.error import URLError

DISCORD_API_BASE = "https://discord.com/api/v10"

def get_config():
    data = {}

    # prefer token from environment if present
    envbase = ["PluralKit", "Bot"]
    for var in ["Token", "ClientId"]:
        for sep in [':', '__']:
            envvar = sep.join(envbase + [var])
            if envvar in os.environ:
                data[var] = os.environ[envvar]

    if "Token" in data and "ClientId" in data:
        return data

    # else fall back to config
    cfg_path = Path(os.getcwd()) / "pluralkit.conf"
    if cfg_path.exists():
        cfg = {}
        with open(str(cfg_path), 'r') as fh:
            cfg = json.load(fh)

        if 'PluralKit' in cfg and 'Bot' in cfg['PluralKit']:
            return cfg['PluralKit']['Bot']

    return None

def main():
    config = get_config()
    if config is None:
        raise ArgumentError("config was not loaded")
    if 'Token' not in config or 'ClientId' not in config:
        raise ArgumentError("config is missing 'Token' or 'ClientId'")

    data = json.dumps(COMMAND_LIST)
    url = DISCORD_API_BASE + f"/applications/{config['ClientId']}/commands"
    req = request.Request(url, method='PUT', data=data.encode('utf-8'))
    req.add_header("Content-Type", "application/json")
    req.add_header("Authorization", f"Bot {config['Token']}")
    req.add_header("User-Agent", "PluralKit (app-commands updater; https://pluralkit.me)")

    try:
        with request.urlopen(req) as resp:
            if resp.status == 200:
                print("Update successful!")
                return 0

    except URLError as resp:
        print(f"[!!!] Update not successful: status {resp.status}", file=sys.stderr)
        print(f"[!!!] Response body below:\n", file=sys.stderr)
        print(resp.read(), file=sys.stderr)
        sys.stderr.flush()

    return 1

if __name__ == "__main__":
    sys.exit(main())