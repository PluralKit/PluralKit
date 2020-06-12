# Command list
This page shows a list of all commands PluralKit supports.

## How to read this page
The first block for each command, **Usage**, shows the format of each command. This is essentially a "template" you'll need to fill in.
The parts you can change are called **arguments**, and they're highlighted.

### Arguments
The command's **Arguments** section will describe how to fill them in, and what you need to put there.

::: details Example of a command with an argument
Here's an example of a command with an argument:
<Cmd>some-command <Arg>this-is-an-argument</Arg></Cmd>

When running this command, fill in the argument like so:
<Cmd>some-command <Arg>My cool text</Arg></Cmd>
:::

### Systems and members
Some commands accept a **target system or member**: 

For **systems**, this can either be a [5-character ID](./ids.md), a `@mention`, or a [Discord user ID](https://support.discord.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID-).  
In many cases you can leave the system out entirely. Instead, it'll just use your own system.

::: details Example of referring to systems
<CmdGroup>
<Cmd comment="Own system">system list</Cmd>
<Cmd comment="By ID">system <Arg>abcde</Arg> list</Cmd>
<Cmd comment="By @mention">system <Arg>@Myriad#1234</Arg> list</Cmd>
<Cmd comment="By Discord user ID">system <Arg>466378653216014359</Arg> list</Cmd>
</CmdGroup>
:::

For **members**, this can be the member's name or their [5-character ID](./ids).

::: details Example of referring to members
<CmdGroup>
<Cmd comment="By name">member <Arg>Myriad</Arg> info</Cmd>
<Cmd comment="By name (with spaces)">member <Arg>"Myriad Kit"</Arg></Cmd>
<Cmd comment="By ID">member <Arg>zxcvb</Arg> info</Cmd>
</CmdGroup>
:::

::: warning
If you're trying to refer to a member with **spaces or emojis** in their name, you'll need to wrap the name in either 'single' or "double" quotes. Alternatively, you can use the [member ID](./ids).
:::

### Flags
**Flags** are small options you can apply to a command to change its behavior.

All flags follow the format `-word` or `-multiple-words` (as in, they all start with a **-**).
Flags **may appear in any order**, and most flags can appear anywhere in the command
(with some exceptions; although placing them before the first argument is usually a safe bet).

A common flag is `-clear`, which is used for most "change" commands. This flag instructs the command to clear a value instead of showing or changing it. For example, to clear your system description, you'd use the flag like this:
<Cmd comment="Clears your system description (note the flag: -clear)">system description -clear</Cmd>

Commands with flags list the flags in the **Flags** section below, along with an explanation of what they do.

## System commands
::: tip
You can use <Cmd inline>s</Cmd> instead of <Cmd inline>system</Cmd> as a short-hand.
:::

<CommandInfo cmd="systemInfo"></CommandInfo>
<CommandInfo cmd="systemNew"></CommandInfo>
<CommandInfo cmd="systemName"></CommandInfo>
<CommandInfo cmd="systemDesc"></CommandInfo>

## Member commands
::: tip
You can use <Cmd inline>m</Cmd> instead of <Cmd inline>member</Cmd> as a short-hand.
:::

## Switch commands
::: tip
You can use <Cmd inline>sw</Cmd> instead of <Cmd inline>switch</Cmd> as a short-hand.
:::