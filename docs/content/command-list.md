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
- `pk;system [system]` - Shows information about a system.
- `pk;system new [name]` - Creates a new system registered to your account.
- `pk;system [system] rename [new name]` - Changes the name of your system.
- `pk;system [system] description [description]` - Changes the description of your system.
- `pk;system [system] avatar [avatar url|@mention|upload]` - Changes the avatar of your system.
- `pk;system [system] banner [image url|upload]` - Changes your system's banner image.
- `pk;system [system] privacy` - Displays your system's current privacy settings.
- `pk;system [system] privacy <subject> <public|private>` - Changes your systems privacy settings.
- `pk;system [system] tag [tag]` - Changes the system tag of your system.
- `pk;system [system] servertag [tag|-enable|-disable]` - Changes your system's tag in the current server, or disables it for the current server.
- `pk;system [system] pronouns [pronouns]` - Changes the pronouns of your system.
- `pk;system proxy [server id] [on|off]` - Toggles message proxying for a specific server.
- `pk;system [system] delete` - Deletes your system.
- `pk;system [system] fronter` - Shows the current fronter of a system.
- `pk;system [system] fronthistory` - Shows the last 10 fronters of a system.
- `pk;system [system] frontpercent [timeframe]` - Shows the aggregated front history of a system within a given time frame.
- `pk;system [system] list` - Shows a paginated list of a system's members.
- `pk;system [system] list -full` - Shows a paginated list of a system's members, with increased detail.
- `pk;find <search term>` - Searches members by name.
- `pk;system [system] find <search term>` - (same as above, but for a specific system)

## Member commands
*Replace `<member>` with a member's name, 5-character ID or display name. For most commands, adding `-clear` will clear/delete the field.*
- `pk;member <member>` - Shows information about a member.
- `pk;member new <name>` - Creates a new system member.
- `pk;member <member> rename <new name>` - Changes the name of a member.
- `pk;member <member> displayname <new display name>` - Changes the display name of a member.
- `pk;member <member> servername <new server name>` - Changes the display name of a member, only in the current server.
- `pk;member <member> description [description]` - Changes the description of a member.
- `pk;member <member> avatar [avatar url|@mention|upload]` - Changes the avatar of a member.
- `pk;member <member> serveravatar [avatar url|@mention|upload]` - Changes the avatar of a member in a specific server.
- `pk;member <name> banner [image url|upload]` - Changes the banner image of a member.
- `pk;member <member> privacy` - Displays a members current privacy settings.
- `pk;member <member> privacy <subject> <public|private>` - Changes a members privacy setting.
- `pk;member <member> proxy [tags]` - Changes the proxy tags of a member. use below add/remove commands for members with multiple tag pairs.
- `pk;member <member> proxy add [tags]` - Adds a proxy tag pair to a member.
- `pk;member <member> proxy remove [tags]` - Removes a proxy tag from a member.
- `pk;member <member> autoproxy [on|off]` - Sets whether a member will be autoproxied when autoproxy is set to latch or front mode.
- `pk;member <member> keepproxy [on|off]` - Sets whether to include a member's proxy tags in the proxied message.
- `pk;member <member> pronouns [pronouns]` - Changes the pronouns of a member.
- `pk;member <member> color [color]` - Changes the color of a member.
- `pk;member <member> birthdate [birthdate|today]` - Changes the birthday of a member.
- `pk;member <member> delete` - Deletes a member.

## Group commands
*Replace `<group>` with a group's name, 5-character ID or display name. For most commands, adding `-clear` will clear/delete the field.*
- `pk;group <group>` - Shows information about a group.
- `pk;group new <name>` - Creates a new group.
- `pk;group list` - Lists all groups in your system.
- `pk;group <group> list` - Lists all members in a group.
- `pk;group <group> random` - Shows the info card of a randomly selected member in a group.
- `pk;group <group> rename <new name>` - Renames a group.
- `pk;group <group> displayname [display name]` - Shows or changes a group's display name.
- `pk;group <group> description [description]` - Shows or changes a group's description.
- `pk;group <group> add <member> [member 2] [member 3...]` - Adds one or more members to a group.
- `pk;group <group> remove <member> [member 2] [member 3...]` - Removes one or more members from a group.
- `pk;group <group> privacy <name|description|icon|visibility|metadata|all> <public|private>` - Changes a group's privacy settings.
- `pk;group <group> icon [icon url|@mention|upload]` - Shows or changes a group's icon.
- `pk;group <group> banner [image url|upload]` - Shows or changes a group's banner image.
- `pk;group <group> delete` - Deletes a group.

## Switching commands
- `pk;switch [member...]` - Registers a switch with the given members.
- `pk;switch out` - Registers a 'switch-out' - a switch with no associated members.
- `pk;switch edit <member...|out>` - Edits the members in the latest switch. 
- `pk;switch move <time>` - Moves the latest switch backwards in time.
- `pk;switch delete` - Deletes the latest switch.
- `pk;switch delete all` - Deletes all logged switches.

## Autoproxy commands
- `pk;autoproxy off` - Disables autoproxying for your system in the current server.
- `pk;autoproxy front` - Sets your system's autoproxy in this server to proxy the first member currently registered as front.
- `pk;autoproxy latch` - Sets your system's autoproxy in this server to proxy the last manually proxied member.
- `pk;autoproxy \<member>` - Sets your system's autoproxy in this server to proxy a specific member.

## Config commands
- `pk;config timezone [location]` - Changes the time zone of your system.
- `pk;config ping <enable|disable>` - Changes your system's ping preferences.
- `pk;config autoproxy timeout [<duration>|off|reset]` - Sets the latch timeout duration for your system.
- `pk;config autoproxy account [on|off]` - Toggles autoproxy globally for the current account.

## Server owner commands
*(all commands here require Manage Server permission)*
- `pk;log channel` - Shows the currently set log channel
- `pk;log channel <channel>` - Sets the given channel to log all proxied messages.
- `pk;log channel -clear` - Clears the currently set log channel.
- `pk;log disable <#channel> [#channel...]` - Disables logging messages posted in the given channel(s) (useful for staff channels and such).
- `pk;log enable <#channel> [#channel...]` - Re-enables logging messages posted in the given channel(s).
- `pk;log show` - Displays the current list of channels where logging is disabled.
- `pk;logclean <on|off>` - Enables or disables [log cleanup](/staff/compatibility/#log-cleanup).
- `pk;blacklist add <#channel> [#channel...]` - Adds the given channel(s) to the proxy blacklist (proxying will be disabled here)
- `pk;blacklist remove <#channel> [#channel...]` - Removes the given channel(s) from the proxy blacklist.

## Utility
- `pk;random [-group]` - Shows the info card of a randomly selected member [or group] in your system.
- `pk;message <message id|message link|reply>` - Looks up information about a proxied message by its message ID or link.
- `pk;invite` - Sends the bot invite link for PluralKit.
- `pk;import` - Imports a data file from PluralKit or Tupperbox.
- `pk;export` - Exports a data file containing your system information.
- `pk;debug permissions [server id]` - [Checks the given server's permission setup](/staff/permissions/#permission-checker-command) to check if it's compatible with PluralKit.
- `pk;debug proxying <message link|reply>` - Checks why your message has not been proxied.
- `pk;edit [message link|reply] <new content>` - Edits a proxied message. Without an explicit message target, will target the last message proxied by your system in the current channel. **Does not support message IDs!**
- `pk;reproxy [message link|reply] <member name|ID>` - Reproxies a message using a different member. Without an explicit message target, will target the last message proxied by your system in the current channel.
- `pk;link <account>` - Links your system to a different account.
- `pk;unlink [account]` - Unlinks an account from your system.

## API
*(for using the [PluralKit API](/api), useful for developers)*
- `pk;token` - DMs you a token for using the PluralKit API.
- `pk;token refresh` - Refreshes your API token and invalidates the old one.
- `pk;s webhook [url]` - Shows or updates the [dispatch webhook](/api/dispatch) URL for your system.

## Help
- `pk;help` - Displays a basic help message describing how to use the bot.
- `pk;help proxy` - Directs you to [this page](/guide/#proxying).
- `pk;system help` - Lists system-related commands.
- `pk;member help` - Lists member-related commands.
- `pk;switch help` - Lists switch-related commands.
- `pk;commands` - Shows inline command documentation, or directs you to this page.