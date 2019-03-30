import json
import os.path

helpfile = None
with open(os.path.dirname(__file__) + "/help.json", "r") as f:
    helpfile = json.load(f)
