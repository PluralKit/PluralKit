---
layout: default
title: Command List
permalink: /commands
description: The full list of all commands in PluralKit, and a short description of what they do.
nav_order: 3
---

# How to read this
Words in **\<angle brackets>** or **[square brackets]** mean fill-in-the-blank. Square brackets mean this is optional. Don't include the actual brackets.

# Commands
## System commands
*Optionally replace `[system]` with a @mention, Discord account ID, or 5-character ID. For most commands, adding `-clear` will clear/delete the field.*
- `pk;system [id]` - Shows information about a system.
- `pk;system new [name]` - Creates a new system registered to your account.
- `pk;system rename [new name]` - Changes the name of your system.
- `pk;system description [description]` - Changes the description of your system.
- `pk;system avatar [avatar url]` - Changes the avatar of your system.
- `pk;system privacy` - Displays your system's current privacy settings.
- `pk;system privacy <subject> <public|private>` - Changes your systems privacy settings.
- `pk;system tag [tag]` - Changes the system tag of your system.
- `pk;system timezone [location]` - Changes the time zone of your system.
- `pk;system proxy [on|off]` - Toggles message proxying for a specific server. 
- `pk;system delete` - Deletes your system.
- `pk;system [system] fronter` - Shows the current fronter of a system.
- `pk;system [system] fronthistory` - Shows the last 10 fronters of a system.
- `pk;system [system] frontpercent [timeframe]` - Shows the aggregated front history of a system within a given time frame.
- `pk;system [system] list` - Shows a paginated list of a system's members.
- `pk;system [system] list -full` - Shows a paginated list of a system's members, with increased detail.
- `pk;find <search term>` - Searches members by name.
- `pk;system [system] find <search term>` - (same as above, but for a specific system)
- `pk;autoproxy [off|front|latch|member]` - Updates the system's autoproxy settings for a given server.
- `pk;link <account>` - Links this system to a different account.
- `pk;unlink [account]` - Unlinks an account from this system.

## Member commands
*Replace `<name>` with a member's name or 5-character ID. For most commands, adding `-clear` will clear/delete the field.*
- `pk;member <name>` - Shows information about a member.
- `pk;member new <name>` - Creates a new system member.
- `pk;member <name> rename <new name>` - Changes the name of a member.
- `pk;member <name> displayname <new display name>` - Changes the display name of a member.
- `pk;member <name> servername <new server name>` - Changes the display name of a member, only in the current server.
- `pk;member <name> description [description]` - Changes the description of a member.
- `pk;member <name> avatar <avatar url|@mention>` - Changes the avatar of a member.
- `pk;member <name> serveravatar <avatar url|@mention>` - Changes the avatar of a member in a specific server.
- `pk;member <name> privacy` - Displays a members current privacy settings.
- `pk;member <name> privacy <subject> <public|private>` - Changes a members privacy setting.
- `pk;member <name> proxy [tags]` - Changes the proxy tags of a member. use below add/remove commands for members with multiple tag pairs.
- `pk;member <name> proxy add [tags]` - Adds a proxy tag pair to a member.
- `pk;member <name> proxy remove [tags]` - Removes a proxy tag from a member.
- `pk;member <name> keepproxy [on|off]` - Sets whether to include a member's proxy tags in the proxied message.
- `pk;member <name> pronouns [pronouns]` - Changes the pronouns of a member.
- `pk;member <name> color [color]` - Changes the color of a member.
- `pk;member <name> birthdate [birthdate]` - Changes the birthday of a member.
- `pk;member <name> delete` - Deletes a member.
- `pk;random` - Shows the member card of a randomly selected member in your system.

## Switching commands
- `pk;switch [member...]` - Registers a switch with the given members.
- `pk;switch move <time>` - Moves the latest switch backwards in time.
- `pk;switch delete` - Deletes the latest switch.
- `pk;switch delete all` - Deletes every logged switch.
- `pk;switch out` - Registers a 'switch-out' - a switch with no associated members.

## Server owner commands
*(all commands here require Manage Server permission)*
- `pk;log channel <channel>` - Sets the given channel to log all proxied messages.
- `pk;log disable <#channel> [#channel...]` - Disables logging messages posted in the given channel(s) (useful for staff channels and such).
- `pk;log enable <#channel> [#channel...]` - Re-enables logging messages posted in the given channel(s).
- `pk;logclean <on/off>` - Enables or disables [log cleanup](/guide#log-cleanup).
- `pk;blacklist add <#channel> [#channel...]` - Adds the given channel(s) to the proxy blacklist (proxying will be disabled here)
- `pk;blacklist remove <#channel> [#channel...]` - Removes the given channel(s) from the proxy blacklist.

## Utility
- `pk;message <message id / message link>` - Looks up information about a proxied message by its message ID or link.
- `pk;invite` - Sends the bot invite link for PluralKit.
- `pk;import` - Imports a data file from PluralKit or Tupperbox.
- `pk;export` - Exports a data file containing your system information.
- `pk;permcheck [server id]` - [Checks the given server's permission setup](/tips#permission-checker-command) to check if it's compatible with PluralKit.

## API
*(for using the [PluralKit API](/api), useful for developers)*
- `pk;token` - DMs you a token for using the PluralKit API.
- `pk;token refresh` - Refreshes your API token and invalidates the old one.

## Help
- `pk;help` - Displays a basic help message describing how to use the bot.
- `pk;help proxy` - Directs you to [this page](/guide#proxying).
- `pk;system help` - Lists system-related commands.
- `pk;member help` - Lists member-related commands.
- `pk;switch help` - Lists switch-related commands.
- `pk;commands` - Directs you to this page!