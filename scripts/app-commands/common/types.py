class MessageCommand(dict):
    COMMAND_TYPE = 3

    def __init__(self, name):
        super().__init__()
        self["type"] = self.__class__.COMMAND_TYPE
        self["name"] = name