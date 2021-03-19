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
- `pk;system [system]` - Looks up information about a system.
- `pk;system new [name]` - Creates a new system.
- `pk;system rename [new name]` - Renames your system.
- `pk;system description [description]` - Changes your system's description.
- `pk;system avatar [url|@mention]` - Changes your system's avatar image.
- `pk;system tag [tag]` - Changes your system's tag.
- `pk;system privacy` - Shows your system's current privacy settings.
- `pk;system privacy <description|members|fronter|fronthistory|all> <public|private>` - Changes your system's privacy settings.
- `pk;system timezone [location]` - Changes your system's timezone.
- `pk;system proxy [on|off]` - Toggles message proxying for a specific server.
- `pk;system delete` - Deletes your system.
- `pk;system [system] list [-full]` - Shows a paginated list of a system's members [with increased detail].
- `pk;system [system] fronter` - Shows a system's current fronter(s).
- `pk;system [system] fronthistory` - Shows a system's paginated front history.
- `pk;system [system] frontpercent [timespan]` - Shows a system's front breakdown.
- `pk;system [system] find [-full] <search term>` - Searches a system's members given a search term [with increased detail].
- `pk;system ping <enable|disable>` - Changes your system's ping preferences.
- `pk;link <account>` - Links your system to another account.
- `pk;unlink [account]` - Unlinks your system from an account.

## Member commands
*Replace `<member>` with a member's name, 5-character ID or display name. For most commands, adding `-clear` will clear/delete the field.*
- `pk;member <member>` - Shows information about a member.
- `pk;member new <name>` - Creates a new member.
- `pk;member <member> rename <new name>` - Renames a member.
- `pk;member <member> displayname [display name]` - Changes a member's display name.
- `pk;member <member> servername [server name]` - Changes a member's display name in the current server.
- `pk;member <member> description [description]` - Changes a member's description.
- `pk;member <member> pronouns [pronouns]` - Changes a member's pronouns.
- `pk;member <member> color [color]` - Changes a member's color.
- `pk;member <member> birthday [birthday]` - Changes a member's birthday.
- `pk;member <member> proxy [add|remove] [example proxy]` - Changes, adds, or removes a member's proxy tags.
- `pk;member <member> autoproxy [on|off]` - Sets whether a member will be autoproxied when autoproxy is set to latch or front mode.
- `pk;member <member> keepproxy [on|off]` - Sets whether to include a member's proxy tags when proxying.
- `pk;member <member> group` - Shows the groups a member is in.
- `pk;member <member> group add <group> [group 2] [group 3...]` - Adds a member to one or more groups.
- `pk;member <member> group remove <group> [group 2] [group 3...]` - Removes a member from one or more groups.
- `pk;member <member> delete` - Deletes a member.
- `pk;member <member> avatar [url|@mention]` - Changes a member's avatar.
- `pk;member <member> serveravatar [url|@mention]` - Changes a member's avatar in the current server.
- `pk;member <member> privacy <name|description|birthday|pronouns|metadata|visibility|all> <public|private>` - Changes a member's privacy settings.
- `pk;random [-group]` - Shows the info card of a randomly selected member [or group] in your system.

## Group commands
*Replace `<name>` with a group's name, 5-character ID or display name. For most commands, adding `-clear` will clear/delete the field.*
- `pk;group <name>` - Looks up information about a group.
- `pk;group list` - Lists all groups in your system.
- `pk;group new <name>` - Creates a new group.
- `pk;group <group> add <member> [member 2] [member 3...]` - Adds one or more members to a group.
- `pk;group <group> remove <member> [member 2] [member 3...]` - Removes one or more members from a group.
- `pk;group <group> list` - Lists all members in a group.
- `pk;group <group> rename <new name>` - Renames a group.
- `pk;group <group> description [description]` - Changes a group's description.
- `pk;group <group> icon [url|@mention]` - Changes a group's icon.
- `pk;group <group> displayname [display name]` - Changes a group's display name.
- `pk;group <group> privacy <description|icon|visibility|all> <public|private>` - Changes a group's privacy settings.
- `pk;group <group> delete` - Deletes a group.
- `pk;group <group> random` - Shows the info card of a randomly selected member in a group.
- `pk;random group` - Shows the info card of a randomly selected group in your system.

## Switching commands
- `pk;switch <member> [member 2] [member 3...]` - Registers a switch.
- `pk;switch out` - Registers a switch with no members.
- `pk;switch move <date/time>` - Moves the latest switch in time.
- `pk;switch delete` - Deletes the latest switch.
- `pk;switch delete all` - Deletes all logged switches.

## Autoproxy commands
- `pk;autoproxy [off|front|latch|member]` - Sets your system's autoproxy mode for the current server.
- `pk;autoproxy timeout [<duration>|off|reset]` - Sets the latch timeout duration for your system.
- `pk;autoproxy account [on|off]` - Toggles autoproxy globally for the current account.

## Server owner commands
- `pk;log channel <channel>` - Designates a channel to post proxied messages to.
- `pk;log channel -clear` - Clears the currently set log channel.
- `pk;log enable all|<channel> [channel 2] [channel 3...]` - Enables message logging in certain channels.
- `pk;log disable all|<channel> [channel 2] [channel 3...]` - Disables message logging in certain channels.
- `pk;logclean [on|off]` - Toggles whether to clean up other bots' log channels.
- `pk;blacklist show` - Displays the current proxy blacklist.
- `pk;blacklist add all|<channel> [channel 2] [channel 3...]` - Adds certain channels to the proxy blacklist.
- `pk;blacklist remove all|<channel> [channel 2] [channel 3...]` - Removes certain channels from the proxy blacklist.
- `pk;permcheck [server id]` - [Checks the given server's permission setup](<./staff/permissions/#permission-checker-command>) to check if it's compatible with PluralKit.

## Utility
- `pk;invite` - Gets a link to invite PluralKit to other servers.
- `pk;import [fileurl|*attachment*]` - Imports system information from a data file created by PluralKit or Tupperbox.
- `pk;export` - Sends you a data file containing your system information.
- `pk;explain` - Explains the basics of systems and proxying.
- `pk;stats` - Prints statistics related to PluralKit, such as resource usage, latency, and registered system/member count.

## API
*(for using the [PluralKit API](https://pluralkit.me/api), useful for developers)*
- `pk;token` - DMs you an access token for the PluralKit API.
- `pk;token refresh` - Refreshes your API token and invalidates the old one.

## Help
- `pk;help` - Displays a basic help message describing how to use the bot.
- `pk;commands <target>` - Shows the list of commands available for use (where `target` is one of `system`, `member`, `group`, `switch`, `log`, `blacklist`).
