# Command list

::: tip HOW TO READ THIS PAGE
Below is a list of all the commands the bot supports.

Highlighted spaces (eg. <Arg>system-name</Arg> ) are **arguments**, and you should **fill in the blank** with the relevant bit of text.
The **Arguments** section below each command describes how to fill it in, and what type of value goes there.

When an argument asks for a **system ID**, you can either fill in a system's [5-character ID](./ids.md), **or** you can fill in a Discord account ID, or even a @mention. For example:
<CmdGroup>
<Cmd comment="Looks up a system with the ID 'exmpl'">system <Arg>exmpl</Arg></Cmd>
<Cmd>system <Arg>466378653216014359</Arg> list</Cmd>
<Cmd>system <Arg>@PluralKit#4020</Arg> fronter</Cmd>
</CmdGroup>

When an argument asks for a **member ID**, you can either fill in a member's [5-character ID](./ids.md), or, *if the member is in your own system*, their name. This means that to target a member in another system, you **must** use their ID.
:::

## System commands
::: tip
You can use <CmdInline>s</CmdInline> instead of <CmdInline>system</CmdInline> as a short-hand.
:::

<CommandInfo cmd="system-info"></CommandInfo>
<CommandInfo cmd="system-new"></CommandInfo>

## Member commands
::: tip
You can use <CmdInline>m</CmdInline> instead of <CmdInline>member</CmdInline> as a short-hand.
:::

## Switch commands
::: tip
You can use <CmdInline>sw</CmdInline> instead of <CmdInline>switch</CmdInline> as a short-hand.
:::