# IDs
Most "things" in PluralKit have a randomly assigned **5-character ID**. When you create a new system or a new member,
an ID will be generated for that system or member.

The IDs are always lowercase letters, and will look something like this: `zxcvb`. They generally won't spell anything readable,
but given their random nature, occasionally they'll produce a word. Consider it a bonus! (unless it's a slur or something rude, in which case [ask me to change it](../support-server.md))

## Finding your IDs
To find your own system ID, look up your own system card using the <Cmd inline>system</Cmd> command. The system ID will display in the card's footer, like so:

TODO: insert example with highlighted ID

To find a member ID, you can similarly look up the member by name using the <Cmd inline>member <Arg>member-name</Arg></Cmd> command, like so:

TODO: insert example with highlighted ID (showing command invocation)


You can also [look at your member list](./listing.md), and each member's ID will be shown on the left-hand side, like so:

TODO: insert example with highlighted ID (showing command invocation)

## Where can I use it?
IDs are the universal way of uniquely referring to a system or member. Most commands will allow you to enter a relevant ID:

<CmdGroup>
<Cmd>system <Arg>system-id</Arg></Cmd>
<Cmd>system <Arg>system-id</Arg> list</Cmd>
<Cmd>member <Arg>member-id</Arg></Cmd>
<Cmd>switch <Arg>member-id-1</Arg> <Arg>member-id-2</Arg></Cmd>
</CmdGroup>

**System IDs** can also be written as either an account `@mention` or a numeric [Discord user ID](https://support.discord.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID-). This will refer to the system linked to that account.

**Member IDs** can be used instead of member names in most commands. This is especially useful if a member has spaces or symbols in the username; using the member ID lets you avoid messing with "quotes", emojis, special keyboards, etc.

::: details Example
Take a member with the name `Myriad "Big Boss" Kit ✨`. This member has the ID `asdfg`.

When running a command on this member (eg. changing their description), both of these will work:

<CmdGroup>
<Cmd>member <Arg>'Myriad "Big Boss" Kit ✨'</Arg> description <Arg>My new description!</Arg></Cmd>
<Cmd>member <Arg>asdfg</Arg> description <Arg>My new description!</Arg></Cmd>
</CmdGroup>
:::