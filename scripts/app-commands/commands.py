from common import *

COMMAND_LIST = [
    MessageCommand("\U00002753 Message info"),
    MessageCommand("\U0000274c Delete message"),
    MessageCommand("\U0001f514 Ping author"),
]

if __name__ == "__main__":
    print(__import__('json').dumps(COMMAND_LIST))