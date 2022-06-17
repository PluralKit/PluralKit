---
layout: default
title: Command List
permalink: /commands
description: The full list of all commands in PluralKit, and a short description of what they do.
nav_order: 3
---

# How to read this
Words in **\<angle brackets>** or **[square brackets]** mean fill-in-the-blank. Square brackets mean this is optional. Don't include the actual brackets.

## Special arguments
Some arguments indicate the use of specific Discord features. These include:

- `@mention`: insert a Discord mention (or "ping")

::: details Mention example
![Mention example](./assets/mention_arg.png)
:::

- `reply`: reply to a previous message

::: details Message reply example
![Message reply example](./assets/reply_arg.png)
:::

- `upload`: upload a file

::: details Upload example
![Upload example](./assets/upload_arg.png)
:::

# Commands
## System commands
*To target a specific system, replace `[system]` with that system's 5-character ID, a Discord account ID, or a @mention - note that system names can not be used here. If no system ID is specified, defaults to targeting your own system. For most commands, adding `-clear` will clear/delete the field.*
- `sp;system [system]` - Shows information about a system.
- `sp;system new [name]` - Creates a new system registered to your account.
- `sp;system [system] rename [new name]` - Changes the name of your system.
- `sp;system [system] description [description]` - Changes the description of your system.
- `sp;system [system] avatar [avatar url|@mention|upload]` - Changes the avatar of your system.
- `sp;system [system] banner [image url|upload]` - Changes your system's banner image.
- `sp;system [system] privacy` - Displays your system's current privacy settings.
- `sp;system [system] privacy <subject> <public|private>` - Changes your systems privacy settings.
- `sp;system [system] tag [tag]` - Changes the system tag of your system.
- `sp;system [system] servertag [tag|-enable|-disable]` - Changes your system's tag in the current server, or disables it for the current server.
- `sp;system [system] pronouns [pronouns]` - Changes the pronouns of your system.
- `sp;system proxy [server id] [on|off]` - Toggles message proxying for a specific server.
- `sp;system [system] delete` - Deletes your system.
- `sp;system [system] fronter` - Shows the current fronter of a system.
- `sp;system [system] fronthistory` - Shows the last 10 fronters of a system.
- `sp;system [system] frontpercent [timeframe]` - Shows the aggregated front history of a system within a given time frame.
- `sp;system [system] list` - Shows a paginated list of a system's members.
- `sp;system [system] list -full` - Shows a paginated list of a system's members, with increased detail.
- `sp;find <search term>` - Searches members by name.
- `sp;system [system] find <search term>` - (same as above, but for a specific system)

## Member commands
*Replace `<member>` with a member's name, 5-character ID or display name. For most commands, adding `-clear` will clear/delete the field.*
- `sp;member <member>` - Shows information about a member.
- `sp;member new <name>` - Creates a new system member.
- `sp;member <member> rename <new name>` - Changes the name of a member.
- `sp;member <member> displayname <new display name>` - Changes the display name of a member.
- `sp;member <member> servername <new server name>` - Changes the display name of a member, only in the current server.
- `sp;member <member> description [description]` - Changes the description of a member.
- `sp;member <member> avatar [avatar url|@mention|upload]` - Changes the avatar of a member.
- `sp;member <member> serveravatar [avatar url|@mention|upload]` - Changes the avatar of a member in a specific server.
- `sp;member <name> banner [image url|upload]` - Changes the banner image of a member.
- `sp;member <member> privacy` - Displays a members current privacy settings.
- `sp;member <member> privacy <subject> <public|private>` - Changes a members privacy setting.
- `sp;member <member> proxy [tags]` - Changes the proxy tags of a member. use below add/remove commands for members with multiple tag pairs.
- `sp;member <member> proxy add [tags]` - Adds a proxy tag pair to a member.
- `sp;member <member> proxy remove [tags]` - Removes a proxy tag from a member.
- `sp;member <member> autoproxy [on|off]` - Sets whether a member will be autoproxied when autoproxy is set to latch or front mode.
- `sp;member <member> keepproxy [on|off]` - Sets whether to include a member's proxy tags in the proxied message.
- `sp;member <member> pronouns [pronouns]` - Changes the pronouns of a member.
- `sp;member <member> color [color]` - Changes the color of a member.
- `sp;member <member> birthdate [birthdate|today]` - Changes the birthday of a member.
- `sp;member <member> delete` - Deletes a member.

## Group commands
*Replace `<group>` with a group's name, 5-character ID or display name. For most commands, adding `-clear` will clear/delete the field.*
- `sp;group <group>` - Shows information about a group.
- `sp;group new <name>` - Creates a new group.
- `sp;group list` - Lists all groups in your system.
- `sp;group <group> list` - Lists all members in a group.
- `sp;group <group> random` - Shows the info card of a randomly selected member in a group.
- `sp;group <group> rename <new name>` - Renames a group.
- `sp;group <group> displayname [display name]` - Shows or changes a group's display name.
- `sp;group <group> description [description]` - Shows or changes a group's description.
- `sp;group <group> add <member> [member 2] [member 3...]` - Adds one or more members to a group.
- `sp;group <group> remove <member> [member 2] [member 3...]` - Removes one or more members from a group.
- `sp;group <group> privacy <name|description|icon|visibility|metadata|all> <public|private>` - Changes a group's privacy settings.
- `sp;group <group> icon [icon url|@mention|upload]` - Shows or changes a group's icon.
- `sp;group <group> banner [image url|upload]` - Shows or changes a group's banner image.
- `sp;group <group> delete` - Deletes a group.

## Switching commands
- `sp;switch [member...]` - Registers a switch with the given members.
- `sp;switch out` - Registers a 'switch-out' - a switch with no associated members.
- `sp;switch edit <member...|out>` - Edits the members in the latest switch. 
- `sp;switch move <time>` - Moves the latest switch backwards in time.
- `sp;switch delete` - Deletes the latest switch.
- `sp;switch delete all` - Deletes all logged switches.

## Autoproxy commands
- `sp;autoproxy off` - Disables autoproxying for your system in the current server.
- `sp;autoproxy front` - Sets your system's autoproxy in this server to proxy the first member currently registered as front.
- `sp;autoproxy latch` - Sets your system's autoproxy in this server to proxy the last manually proxied member.
- `sp;autoproxy \<member>` - Sets your system's autoproxy in this server to proxy a specific member.

## Config commands
- `sp;config timezone [location]` - Changes the time zone of your system.
- `sp;config ping <enable|disable>` - Changes your system's ping preferences.
- `sp;config autoproxy timeout [<duration>|off|reset]` - Sets the latch timeout duration for your system.
- `sp;config autoproxy account [on|off]` - Toggles autoproxy globally for the current account.

## Server owner commands
*(all commands here require Manage Server permission)*
- `sp;log channel` - Shows the currently set log channel
- `sp;log channel <channel>` - Sets the given channel to log all proxied messages.
- `sp;log channel -clear` - Clears the currently set log channel.
- `sp;log disable <#channel> [#channel...]` - Disables logging messages posted in the given channel(s) (useful for staff channels and such).
- `sp;log enable <#channel> [#channel...]` - Re-enables logging messages posted in the given channel(s).
- `sp;log show` - Displays the current list of channels where logging is disabled.
- `sp;logclean <on|off>` - Enables or disables [log cleanup](/staff/compatibility/#log-cleanup).
- `sp;blacklist add <#channel> [#channel...]` - Adds the given channel(s) to the proxy blacklist (proxying will be disabled here)
- `sp;blacklist remove <#channel> [#channel...]` - Removes the given channel(s) from the proxy blacklist.

## Utility
- `sp;random [-group]` - Shows the info card of a randomly selected member [or group] in your system.
- `sp;message <message id|message link|reply>` - Looks up information about a proxied message by its message ID or link.
- `sp;invite` - Sends the bot invite link for PluralKit.
- `sp;import` - Imports a data file from PluralKit or Tupperbox.
- `sp;export` - Exports a data file containing your system information.
- `sp;debug permissions [server id]` - [Checks the given server's permission setup](/staff/permissions/#permission-checker-command) to check if it's compatible with PluralKit.
- `sp;debug proxying <message link|reply>` - Checks why your message has not been proxied.
- `sp;edit [message link|reply] <new content>` - Edits a proxied message. Without an explicit message target, will target the last message proxied by your system in the current channel. **Does not support message IDs!**
- `sp;reproxy [message link|reply] <member name|ID>` - Reproxies a message using a different member. Without an explicit message target, will target the last message proxied by your system in the current channel.
- `sp;link <account>` - Links your system to a different account.
- `sp;unlink [account]` - Unlinks an account from your system.

## API
*(for using the [PluralKit API](/api), useful for developers)*
- `sp;token` - DMs you a token for using the PluralKit API.
- `sp;token refresh` - Refreshes your API token and invalidates the old one.
- `sp;s webhook [url]` - Shows or updates the [dispatch webhook](/api/dispatch) URL for your system.

## Help
- `sp;help` - Displays a basic help message describing how to use the bot.
- `sp;help proxy` - Directs you to [this page](/guide/#proxying).
- `sp;system help` - Lists system-related commands.
- `sp;member help` - Lists member-related commands.
- `sp;switch help` - Lists switch-related commands.
- `sp;commands` - Shows inline command documentation, or directs you to this page.