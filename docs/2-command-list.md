---
layout: default
title: Command List
permalink: /commands
---

# How to read this
Words in <angle brackets> are *required parameters*. Words in [square brackets] are *optional parameters*. Words with ellipses... indicate multiple repeating parameters.

# Commands
## System commands
- `pk;system [id]` - Shows information about a system.
- `pk;system new [name]` - Creates a new system registered to your account.
- `pk;system rename [new name]` - Changes the description of your system.
- `pk;system description [description]` - Changes the description of your system.
- `pk;system avatar [avatar url]` - Changes the avatar of your system.
- `pk;system tag [tag]` - Changes the system tag of your system.
- `pk;system timezone [location]` - Changes the time zone of your system.
- `pk;system delete` - Deletes your system.
- `pk;system [id] fronter` - Shows the current fronter of a system.
- `pk;system [id] fronthistory` - Shows the last 10 fronters of a system.
- `pk;system [id] frontpercent [timeframe]` - Shows the aggregated front history of a system within a given time frame.
- `pk;system [id] list` - Shows a paginated list of a system's members.
- `pk;system [id] list full` - Shows a paginated list of a system's members, with increased detail.
- `pk;link <account>` - Links this system to a different account.
- `pk;unlink [account]` - Unlinks an account from this system.
## Member commands
- `pk;member <name>` - Shows information about a member.
- `pk;member new <name>` - Creates a new system member.
- `pk;member <name> rename <new name>` - Changes the name of a member.
- `pk;member <name> description [description` - Changes the description of a member.
- `pk;member <name> avatar [avatar url]` - Changes the avatar of a member.
- `pk;member <name> proxy [tags]` - Changes the proxy tags of a member.
- `pk;member <name> pronouns [pronouns]` - Changes the pronouns of a member.
- `pk;member <name> color [color]` - Changes the color of a member.
- `pk;member <name> birthdate [birthdate]` - Changes the birthday of a member.
- `pk;member <name> delete` - Deletes a member. 
## Switching commands
- `pk;switch [member...]` - Registers a switch with the given members.
- `pk;switch move <time>` - Moves the latest switch backwards in time.
- `pk;switch delete` - Deletes the latest switch.
- `pk;switch out` - Registers a 'switch-out' - a switch with no associated members.
## Utility
- `pk;log <channel>` - Sets the channel to log all proxied messages.
- `pk;message <message id>` - Looks up information about a proxied message by its message ID.
- `pk;invite` - Sends the bot invite link for PluralKit.
- `pk;import` - Imports a data file from PluralKit or Tupperbox.
- `pk;export` - Exports a data file containing your system information.
## API
- `pk;token` - DMs you a token for using the PluralKit API.
- `pk;token refresh` - Refreshes your API token and invalidates the old one.
## Help
- `pk;help` - Displays a basic help message describing how to use the bot.
- `pk;help proxy` - Directs you to [this page](/guide#proxying).
- `pk;commands` - Directs you to this page!