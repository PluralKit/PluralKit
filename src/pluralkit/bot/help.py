command_notes = """
__**Command notes**__
Parameters in <angle brackets> are required, [square brackets] are optional. **Do not include the brackets themselves when using the command.**
Member references can be a member ID or, for your own system, a member name. **If a member name contains spaces, it must be wrapped in "quotation marks" when being referenced (but not during creation).**
Leaving an optional parameter blank will often clear the relevant value.
""".strip()

system_commands = """
__**System commands**__
Commands for adding, removing, editing, and linking systems, as well as querying fronter and front history.
```
pk;system [system]
pk;system new [system name]
pk;system rename [new name]
pk;system description [new description]
pk;system avatar [new avatar URL]
pk;system tag [new system tag]
pk;system timezone [city/town]
pk;system delete
pk;system [system] fronter
pk;system [system] fronthistory
pk;system [system] frontpercent
pk;system [system] list
pk;link <@other account>
pk;unlink
```
""".strip()

member_commands = """
__**Member commands**__
Commands for adding, removing, and modifying members, as well as adding, removing and moving switches.
```
pk;member new <member name>
pk;member <member>
pk;member <member> rename <new name>
pk;member <member> description [new description]
pk;member <member> avatar [new avatar URL/@username]
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
**Please bear in mind that your avatar image has to have 1 dimension 1024 pixels or less, i.e. 1024x2000 or 2500x1024, and be 1 MB or less in size otherwise it will not stick!**
""".strip()

help_commands = """
__**Help commands**__
```
pk;help
pk;help commands
pk;help system
pk;help member
pk;help proxy
```""".strip()

other_commands = """
__**Other commands**__
```
pk;log <log channel>
pk;message <message ID>
pk;invite
pk;import
pk;export
pk;token
pk;token refresh
```
""".strip()

all_commands = """
{}
{}
{}
{}
{}
""".strip().format(system_commands, member_commands + "\n", help_commands, other_commands, command_notes)

proxy_guide = """
__**Proxying**__
Proxying through PluralKit lets system members have their own faux-account with their name and avatar.
You'll type a message from your account in *proxy tags*, and PluralKit will recognize those tags and repost the message with the proper details, with the minor caveat of having the **[BOT]** icon next to the name (this is a Discord limitation and cannot be circumvented).

To set up a member's proxy tag, use the `pk;member <name> proxy [example match]` command.

You'll need to give the bot an "example match" containing the word `text`. Imagine you're proxying the word "text", and add that to the end of the command.
For example: `pk;member John proxy [text]`. That will set the member John up to use square brackets as proxy tags.
Now saying something like `[hello world]` will proxy the text "hello world" with John's name and avatar.
You can also use other symbols, letters, numbers, et cetera, as prefixes, suffixes, or both. `J:text`, `$text` and `text]` are also examples of valid example matches.

**Notes**
You can delete a proxied message by reacting to it with the :x: emoji from the sender's account.
""".strip()

root = """
__**PluralKit**__
PluralKit is a bot designed for plural communities on Discord. It allows you to register systems, maintain system information, set up message proxying, log switches, and more.

**Who's this for? What are systems?**
Put simply, a system is a person that shares their body with at least 1 other sentient "self". This may be a result of having a dissociative disorder like DID/OSDD or a practice known as Tulpamancy, but people that aren't tulpamancers or undiagnosed and have headmates are also systems.

**Why are people's names saying [BOT] next to them? What's going on?**
These people are not actually bots, this is simply a caveat to the message proxying feature of PluralKit.
Type `pk;help proxy` for an in-depth explanation.

__**Getting started**__
To get started using the bot, try running the following commands.
**1**. `pk;system new` - Create a system if you haven't already
**2**. `pk;member add John` - Add a new member to your system
**3**. `pk;member John proxy [text]` - Set up square brackets as proxy tags
**4**. You're done!

**5**. Optionally, you may set an avatar from the URL of an image with:
`pk;member John avatar [link to image]`
Type `pk;help member` for more information.

**Useful tip:**
You can delete a proxied message by reacting to it with :x: (if you sent it!).


__**More information**__
For a full list of commands, type `pk;help commands`.
For a more in-depth explanation of message proxying, type `pk;help proxy`.
If you're an existing user of the Tupperbox proxy bot, type `pk;import` to import your data from there.

__**Support server**__
We also have a Discord server for support, discussion, suggestions, announcements, etc: <https://discord.gg/PczBt78>
""".strip()
