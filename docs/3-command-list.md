---
layout: default
title: Command List
permalink: /commands
description: The full list of all commands in PluralKit, and a short description of what they do.
---

# How to read this
Words in \<angle brackets> are *required parameters*. Words in [square brackets] are *optional parameters*. Words with ellipses... indicate multiple repeating parameters. Note that **you should not include the brackets in the actual command**.

# Commands
## System commands
- `pk;system [id]` - Shows information about a system.
- `pk;system new [name]` - Creates a new system registered to your account.
- `pk;system rename [new name]` - Changes the name of your system.
- `pk;system description [description]` - Changes the description of your system.
- `pk;system avatar [avatar url]` - Changes the avatar of your system.
- `pk;system tag [tag]` - Changes the system tag of your system.
- `pk;system timezone [location]` - Changes the time zone of your system.
- `pk;system proxy [on|off]` - Toggles message proxying for a specific server. 
- `pk;system delete` - Deletes your system.
- `pk;system [id] fronter` - Shows the current fronter of a system.
- `pk;system [id] fronthistory` - Shows the last 10 fronters of a system.
- `pk;system [id] frontpercent [timeframe]` - Shows the aggregated front history of a system within a given time frame.
- `pk;system [id] list` - Shows a paginated list of a system's members.
- `pk;system [id] list full` - Shows a paginated list of a system's members, with increased detail.
- `pk;autoproxy [off|front|latch|member]` - Updates the system's autoproxy settings for a given server.
- `pk;link <account>` - Links this system to a different account.
- `pk;unlink [account]` - Unlinks an account from this system.
## Member commands
- `pk;member <name>` - Shows information about a member.
- `pk;member new <name>` - Creates a new system member.
- `pk;member <name> rename <new name>` - Changes the name of a member.
- `pk;member <name> displayname <new display name>` - Changes the display name of a member.
- `pk;member <name> servername <new server name>` - Changes the display name of a member, only in the current serve.
- `pk;member <name> description [description]` - Changes the description of a member.
- `pk;member <name> avatar <avatar url|@mention|clear>` - Changes the avatar of a member.
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
## Utility
- `pk;log <channel>` - Sets the channel to log all proxied messages.
- `pk;message <message id>` - Looks up information about a proxied message by its message ID.
- `pk;invite` - Sends the bot invite link for PluralKit.
- `pk;import` - Imports a data file from PluralKit or Tupperbox.
- `pk;export` - Exports a data file containing your system information.
- `pk;permcheck [server id]` - [Checks the given server's permission setup](/tips#permission-checker-command) to check if it's compatible with PluralKit.
## API
- `pk;token` - DMs you a token for using the PluralKit API.
- `pk;token refresh` - Refreshes your API token and invalidates the old one.
## Help
- `pk;help` - Displays a basic help message describing how to use the bot.
- `pk;help proxy` - Directs you to [this page](/guide#proxying).
- `pk;commands` - Directs you to this page!