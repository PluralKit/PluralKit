categories = ("Help categories", """`pk;help system` - Details on system configuration.
`pk;help member` - Details on member configuration.
`pk;help proxy` - Details on message proxying.
`pk;help switch` - Details on switch logging.
`pk;help mod` - Details on moderator operations.
`pk;help import` - Details on data import from other services.""")
getting_started = ("Getting started", """To get started using the bot, try running the following commands:
**1**. `pk;system new` - Create a system if you haven't already
**2**. `pk;member add John` - Add a new member to your system
**3**. `pk;member proxy John [text]` - Set up square brackets as proxy tags 
**4**. You're done! See the other help pages for more commands.""")
discord_link = (
    "Discord",
    """For feedback, bug reports, suggestions, or just chatting, join our Discord: https://discord.gg/PczBt78""")
registering_system = ("Registering a new system",
                      """To use PluralKit, you must register a system for your account. You can use the `pk;system new` command for this. You can optionally add a system name after the command.""")
lookup_system = ("Looking up a system", """To look up a system's details, you can use the `pk;system` command.
        
For example:
`pk;system` - Shows details of your own system.
`pk;system abcde` - Shows details of the system with the ID `abcde`.
`pk;system @JohnsAccount` - Shows details of the system linked to @JohnsAccount.""")
edit_system = ("Editing system properties", """You can use the `pk;system` commands to change your system properties. The properties you can change are name, description, and tag.
        
For example:
`pk;system name My System` - sets your system name to "My System".
`pk;system description A really cool system.` - sets your system description.
`pk;system tag [MS]` - Sets the tag (which will be displayed after member names in messages) to "[MS]".
`pk;system avatar https://placekitten.com/400/400` - Changes your system's avatar to a linked image.

If you don't specify any value, the property will be cleared.""")
link_account = ("Linking accounts", """If your system has multiple accounts, you can link all of them to your system, and you can use the bot from all of those accounts.
        
For example:
`pk;system link @MyOtherAccount` - Links @MyOtherAccount to your system.

You'll need to confirm the link from the other account.""")
unlink_accounts = (
    "Unlinking accounts", """If you need to unlink an account, you can do that with the `pk;system unlink` command.""")
add_member = ("Adding a new member", """To add a new member to your system, use the `pk;member new` command. You'll need to add a member name.

For example:
`pk;member new John`""")
lookup_member = ("Looking up a member", """To look up a member's details, you can use the `pk;member` command.

For example:
`pk;member John` - Shows details of the member in your system named John.
`pk;member abcde` - Shows details of the member with the ID `abcde`.

You can use member IDs to look up members in other systems.""")
edit_member = ("Editing member properties", """You can use the `pk;member` commands to change a member's properties. The properties you can change are name, description, color, pronouns, birthdate and avatar.

For example:
`pk;member rename John Joe` - Changes John's name to Joe.
`pk;member description John Pretty cool dude.` - Changes John's description.
`pk;member color John #ff0000` - Changes John's color to red.
`pk;member pronouns John he/him` - Changes John's pronouns.
`pk;member birthdate John 1996-02-27` - Changes John's birthdate to Feb 27, 1996. (Must be YYYY-MM-DD format).
`pk;member birthdate John 02-27` - Changes John's birthdate to February 27th, with no year.
`pk;member avatar John https://placekitten.com/400/400` - Changes John's avatar to a linked image.
`pk;member avatar John @JohnsAccount` - Changes John's avatar to the avatar of the mentioned account.

If you don't specify any value, the property will be cleared.""")
remove_member = ("Removing a member", """If you want to delete a member, you can use the `pk;member delete` command.
        
For example:
`pk;member delete John`

You will need to confirm the deletion.""")
member_proxy = ("Setting up member proxying", """To register a member for proxying, use the `pk;member proxy` command.

You will need to pass an "example proxy" message containing "text", surrounded by the brackets or prefixes you want to select.
        
For example:
`pk;member proxy John [text]` - Registers John to use [square brackets] as proxy brackets.
`pk;member proxy John J:text` - Registers John to use the prefix "J:".

After setting proxy tags, you can use them in any message, and they'll be interpreted by the bot and proxied appropriately.""")
system_tag = ("Setting your system tag", """To set your system tag, use the `pk;system tag` command.
        
The tag is appended to the name of all proxied messages.

For example:
`pk;system tag [MS]` - Sets your system tag to "[MS]".
`pk;system tag :heart:` - Sets your system tag to the heart emoji.

Note you can only use default Discord emojis, not custom server emojis.""")
message_lookup = ("Looking up a message", """You can look up a message by its ID using the `pk;message` command.
        
For example:
`pk;message 467638937402212352` - Shows information about the message by that ID.

To get a message ID, turn on Developer Mode in your client's Appearance settings, right click, and press "Copy ID".""")
message_delete = ("Deleting messages",
                  """You can delete your own messages by reacting with the ❌ emoji on it. Note that this only works on messages sent from your account.""")
switch_register = ("Registering a switch", """To log a switch in your system, use the `pk;switch` command.
        
For example:
`pk;switch John` - Registers a switch with John as fronter.
`pk;switch John Jill` - Registers a switch John and Jill as co-fronters.""")
switch_out = ("Switching out", """You can use the `pk;switch out` command to register a switch with no one in front.""")
switch_move = ("Moving a switch", """You can move the latest switch you have registered using the `pk;switch move` command.
        
This is useful if you log the switch a while after it happened, and you want to properly backdate it in the history.

For example:
`pk;switch move 10 minutes ago` - Moves the latest switch to 10 minutes ago
`pk;switch move 11pm EST` - Moves the latest switch to 11pm EST

Note that you can't move the switch further back than the second-last logged switch, and you can't move a switch to a time in the future.

The default time zone for absolute times is UTC, but you can specify other time zones in the command itself, as given in the example.""")
front_history = ("Viewing fronting history", """To view front history, you can use the `pk;front` and `pk;front history` commands.
        
For example:
`pk;front` - Shows the current fronter(s) in your own system.
`pk;front abcde` - Shows the current fronter in the system with the ID `abcde`.
`pk;front history` - Shows the past 10 switches in your own system.
`pk;front history @JohnsAccount` - Shows the past 10 switches in the system linked to @JohnsAccount.""")
front_breakdown = ("Viewing a front breakdown", """To see a per-member breakdown of your switches, use the `pk;front percent` command. You can optionally give it a time limit to only count switches after that point.

For example:
`pk;front percent` - Shows a front breakdown for your system since you started logging switches
`pk;front percent 1 day` - Shows a front breakdown for your system for the past day
`pk;front percent Jan 1st 2018` - Shows a front breakdown for your system since January 1st, 2018

Note that the percentages don't necessarily add up to 100%, as multiple members can be listed as fronting at a time.""")
logging_channel = ("Setting up a logging channel", """To designate a channel for the bot to log posted messages to, use the `pk;log` command.
        
For example:
`pk;log #message-log` - Configures the bot to log to #message-log.""")
tupperware_import = ("Importing from Tupperware", """If you already have a registered system on Tupperware, you can use the `pk;import tupperware` command to import it into PluralKit.
        
Note the command only works on a server and channel where the Tupperware bot is already present.""")
help_pages = {
    None: [
        (None,
         """PluralKit is a bot designed for plural communities on Discord. It allows you to register systems, maintain system information, set up message proxying, log switches, and more."""),
        getting_started,
        categories,
        discord_link
    ],
    "system": [
        registering_system,
        lookup_system,
        edit_system,
        link_account,
        unlink_accounts
    ],
    "member": [
        add_member,
        lookup_member,
        edit_member,
        remove_member
    ],
    "proxy": [
        member_proxy,
        system_tag,
        message_lookup,
        message_delete
    ],
    "switch": [
        switch_register,
        switch_out,
        switch_move,
        front_history,
        front_breakdown
    ],
    "mod": [
        (None,
         "Note that all moderation commands require you to have administrator privileges on the server they're used on."),
        logging_channel
    ],
    "import": [
        tupperware_import
    ]
}
