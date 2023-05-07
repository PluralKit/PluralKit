from common import *

COMMAND_LIST = [
    MessageCommand("Message info"),
    MessageCommand("Delete message"),
    MessageCommand("Ping message author"),
]

if __name__ == "__main__":
    print(__import__('json').dumps(COMMAND_LIST))