system_commands = """
**System commands**
Commands for adding, removing, editing, and linking systems, as well as querying fronter and front history.
```
pk;system [system]
pk;system new [system name
pk;system rename [new name]
pk;system description [new description]
pk;system avatar [new avatar url]
pk;system tag [new system tag]
pk;system timezone [time zone name]
pk;system delete
pk;system [system] fronter
pk;system [system] fronthistory
pk;system [system] frontpercent
pk;link <other account>
pk;unlink
```
""".strip()

member_commands = """
**Member commands**
Commands for adding, removing, and modifying members, as well as adding, removing and moving switches.
```
pk;member new <member name>
pk;member <member>
pk;member <member> rename <new name>
pk;member <member> description [new description]
pk;member <member> avatar [new avatar url]
pk;member <member> proxy [example match]
pk;member <member> pronouns [new pronouns]
pk;member <member> color [new color]
pk;member <member> birthday [new birthday]
pk;member <member> delete
pk;switch <member> [<other member>...]
pk;switch move <time to move>
pk;switch out
pk;switch delete
```
""".strip()

help_commands = """
**Help commands**
```
pk;help
pk;help commands
pk;help system
pk;help member
pk;help proxy
```""".strip()

other_commands = """
**Other commands**
```
pk;log <log channel>
pk;message <message id>
pk;invite
pk;import
pk;export
pk;token
pk;token refresh
```
""".strip()

command_notes = """
**Command notes**
Parameters in <angle brackets> are required, [square brackets] are optional .
Member references can be a member ID or, for your own system, a member name.
Leaving an optional parameter blank will often clear the relevant value.
""".strip()

all_commands = """
{}
{}
{}
{}
{}
""".strip().format(system_commands, member_commands, help_commands, other_commands, command_notes)

proxy_guide = """
**Proxying**
Proxying through PluralKit lets system members have their own faux-account with their name and avatar.
You'll type a message from your account in *proxy tags*, and PluralKit will recognize those tags and repost the message with the proper details.

To set up a member's proxy tag, use the `pk;member <name> proxy [example match]` command.

You'll need to give the bot an "example match". Imagine you're proxying the word "text", and add that to the end.
For example: `pk;member John proxy [text]`. That will set the member John up to use square brackets as proxy tags.
Now saying something like `[hello world]` will proxy the text "hello world" with John's name and avatar.
You can also use other symbols, letters, numbers, et cetera, as prefixes, suffixes, or both. `J:text`, `$text` and `text]` are also examples of valid example matches.

**Notes**
You can delete a proxied message by reacting to it with the :x: emoji from the sender's account.
""".strip()

root = """
**PluralKit**
PluralKit is a bot designed for plural communities on Discord. It allows you to register systems, maintain system information, set up message proxying, log switches, and more.

**Getting started**
To get started using the bot, try running the following commands.
**1**. `pk;system new` - Create a system if you haven't already
**2**. `pk;member add John` - Add a new member to your system
**3**. `pk;member John proxy [text]` - Set up square brackets as proxy tags
**4**. You're done!

**More information**
For a full list of commands, type `pk;help commands`.
For a more in-depth explanation of proxying, type `pk;help proxy`.
If you're an existing user of the Tupperware proxy bot, type `pk;import` to import your data from there.

**Support server**
We also have a Discord server for support, discussion, suggestions, announcements, etc: <https://discord.gg/PczBt78>
""".strip()
