help_pages = {
    None: [
        (None,
        """PluralKit is a bot designed for plural communities on Discord. It allows you to register systems, maintain system information, set up message proxying, log switches, and more."""),
        ("Getting started",
        """To get started, set up a system with `pk;system new`. Then, inspect the other help pages for further instructions."""),
        ("Help categories",
        """`pk;help system` - Details on system configuration.
`pk;help member` - Details on member configuration.
`pk;help proxy` - Details on message proxying.
`pk;help switch` - Details on switch logging.
`pk;help mod` - Details on moderator operations.""")
    ],
    "system": [
        ("Registering a new system",
        """To use PluralKit, you must register a system for your account. You can use the `pk;system new` command for this. You can optionally add a system name after the command."""),
        ("Looking up a system",
        """To look up a system's details, you can use the `pk;system` command.
        
For example:
`pk;system` - Shows details of your own system.
`pk;system abcde` - Shows details of the system with the ID `abcde`.
`pk;system @JohnsAccount` - Shows details of the system linked to @JohnsAccount."""),
        ("Editing system properties",
        """You can use the `pk;system set` command to change your system properties. The properties you can change are name, description, and tag.
        
For example:
`pk;system set name My System` - sets your system name to "My System".
`pk;system set description A really cool system.` - sets your system description.
`pk;system set tag [MS]` - Sets the tag (which will be displayed after member names in messages) to "[MS]"."""),
        ("Linking accounts",
        """If your system has multiple accounts, you can link all of them to your system, and you can use the bot from all of those accounts.
        
For example:
`pk;system link @MyOtherAccount` - Links @MyOtherAccount to your system.

You'll need to confirm the link from the other account."""),
        ("Unlinking accounts",
        """If you need to unlink an account, you can do that with the `pk;system unlink` command.""")
    ],
    "member": [
        ("Adding a new member",
"""To add a new member to your system, use the `pk;member new` command. You'll need to add a member name.

For example:
`pk;member new John`"""),
        ("Looking up a member",
        """To look up a member's details, you can use the `pk;member` command.

For example:
`pk;member John` - Shows details of the member in your system named John.
`pk;member abcde` - Shows details of the member with the ID `abcde`.

You can use member IDs to look up members in other systems."""),
        ("Editing member properties",
        """You can use the `pk;member set` command to change a member's properties. The properties you can change are name, description, color, pronouns, birthdate and avatar.

For example:
`pk;member set John name Joe` - Changes John's name to Joe.
`pk;member set John description Pretty cool dude.` - Changes John's description.
`pk;member set John color #ff0000` - Changes John's color to red.
`pk;member set John pronouns he/him` - Changes John's pronouns.
`pk;member set John birthdate 1996-02-27` - Changes John's birthdate to Feb 27, 1996. (Must be YYYY-MM-DD format).
`pk;member set John avatar https://placekitten.com/400/400` - Changes John's avatar to a linked image.
`pk;member set John avatar @JohnsAccount` - Changes John's avatar to the avatar of the mentioned account."""),
        ("Removing a member",
        """If you want to delete a member, you can use the `pk;member delete` command.
        
For example:
`pk;member delete John`

You will need to confirm the deletion.""")
    ],
    "proxy": [
        ("Setting up member proxying",
        """To register a member for proxying, use the `pk;member proxy` command.
        
For example:
`pk;member proxy John [text]` - Registers John to use [square brackets] as tags.
`pk;member proxy John J:text` - Registers John to use the prefix "J:".

After setting proxy tags, you can use them in any message, and they'll be interpreted by the bot and proxied appropriately."""),
        ("Setting your system tag",
        """To set your system tag, use the `pk;system set tag` command.
        
The tag is appended to the name of all proxied messages.

For example:
`pk;system set tag [MS]` - Sets your system tag to "[MS]".
`pk;system set tag :heart:` - Sets your system tag to the heart emoji.

Note you can only use default Discord emojis, not custom server emojis."""),
        ("Looking up a message",
        """You can look up a message by its ID using the `pk;message` command.
        
For example:
`pk;message 467638937402212352` - Shows information about the message by that ID.

To get a message ID, turn on Developer Mode in your client's Appearance settings, right click, and press "Copy ID"."""),
        ("Deleting messages",
        """You can delete your own messages by reacting with the ❌ emoji on it. Note that this only works on messages sent from your account.""")
    ],
    "switch": [
        ("Registering a switch",
        """To log a switch in your system, use the `pk;switch` command.
        
For example:
`pk;switch John` - Registers a switch with John as fronter.
`pk;switch John Jill` - Registers a switch John and Jill as co-fronters."""),
        ("Switching out",
        """You can use the `pk;switch out` command to register a switch-out."""),
        ("Viewing fronting history",
        """To view front history, you can use the `pk;system fronter` and `pk;system fronthistory` commands.
        
For example:
`pk;system fronter` - Shows the current fronter(s) in your own system.
`pk;system fronter abcde` - Shows the current fronter in the system with the ID `abcde`.
`pk;system fronthistory` - Shows the past 10 switches in your own system.
`pk;system fronthistory @JohnsAccount` - Shows the past 10 switches in the system linked to @JohnsAccount.""")
    ],
    "mod": [
        (None, "Note that all moderation commands require you to have administrator privileges on the server they're used on."),
        ("Setting up a logging channel",
        """To designate a channel for the bot to log posted messages to, use the `pk;mod log` command.
        
For example:
`pk;mod log #message-log` - Configures the bot to log to #message-log.""")
    ]
}