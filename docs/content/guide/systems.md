---
systemName: My System Name
---

# Systems
A *system* is equivalent to an account. Every user of PluralKit must have a system registered to use the bot.

## System IDs
Each system has a **system ID** - a randomly generated string of 5 letters. You can use this ID to unambiguously refer to a system in commands.

You can see your system ID by running <Cmd inline>system</Cmd> and looking at the embed footer (at the bottom of the response).

## Creating a system
To create a system, use the <Cmd inline>system new</Cmd> command with an optional system name:

<CmdGroup>
<Cmd>system new</Cmd>
<Cmd>system new <Arg>My System Name</Arg></Cmd>
</CmdGroup>

## Editing your system

### System description
You can set your system description using the following command:
<Cmd>system description <Arg>My cool system description goes here.</Arg></Cmd>

## Linking accounts
A system isn't necessarily tied to a single account. A system can be **linked** to another account, and all linked accounts can run commands on behalf of the system.

To link your system to another account, use the following command:

<Cmd>link <Arg>@NameOfAccount#1234</Arg></Cmd>

The other account will need to confirm the link by pressing a reaction within five minutes.

Should you want to unlink an account, use the equivalent unlink account:

<Cmd>unlink <Arg>@NameOfAccount#1234</Arg></Cmd>

You can unlink your own account too (both by mentioning, or using <Cmd inline>unlink</Cmd> with no account). While you can't unlink the only linked account, be careful not to lock yourself out of your system by other means :slightly_smiling_face:

::: tip
On both of these commands, you can also supply a user ID. This is useful when you want to unlink an already-deleted account, for example.
:::