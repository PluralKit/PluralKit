---
title: Changelog
permalink: /changelog
---

# Changelog

the below is a lightly edited copy of the changelog messages posted on discord. if you want to get notifications when new changelogs are posted, [join the support server](https://discord.gg/PczBt78) and "follow" the [#changelog](https://discord.com/channels/466707357099884544/524660142499823627) channel, or grab the [@Changelog](https://discord.com/channels/466707357099884544/641807196056715294/846538706525749248) role! 

a more complete list of code changes can be found [in the git repo](https://github.com/pluralkit/pluralkit/commits/main)

## 2025-01-01

### Added
- Server admin command to make all PK messages in said server use @silent (aka, not send push notifications)
- When you delete your system there is now a **24 hour** grace period where we keep your images stored. After that if you have not reimported they will be deleted and not recoverable. 
### Fixed
- More error codes now show human readable errors
- Many things added or fixed in docs
### Dev Changes
- We are now on .NET 9 🎉

## 2024-12-05

### Added
- pk;edit -clear-attachments flag
- Raw and plaintext flags for all image commands
- Anabelle logclean support
- Server-specific name format
- Alternate proxy switch mode "add" (note: the original default mode has been changed to "new" (although "on" will still work as well)

### Fixed
- Proxy name format now works even without a tag set
- Serverconfig tag enforcement now checks name formatting
- Nameformat command is now able to be cleared
- You can now look at banners of members not in your system

## 2024-11-10

As a reminder, PluralKit's new [Terms of Service](<https://pluralkit.me/terms-of-service>) and [Privacy Policy](<https://pluralkit.me/privacy>) are now in effect.

### Added
- config setting to automatically log a switch when proxying as a different member
- config setting for proxy name templating
- log cleanup support for Maki and Sapphire bots
- plaintext output embeds now show entity IDs
- separate privacy setting for banners, instead of being included in description privacy
- moved server configuration commands to `pk;serverconfig`

### Fixed
- group/member name privacy is now respected in search queries (it is no longer possible to search for private names you don't have access to)
- IDs are no longer padded in lists when the current page only has 5-character IDs
- a security issue where accounts without a registered system could delete messages from other systems was fixed
- due to confusion, "r" alias was updated to only reference the _random_ command rather than _remove_ commands
- clearing `split ids` and `capitalize ids` config settings now sets the correct default value
- `pk;msg` command now respects id display settings
- a few clear commands now check entity ownership before (instead of after) asking for confirmation

## 2024-10-05

~~__We are currently trying out__ making all PK messages silent, aka making them not give push notifications. This is meant to avoid doubling push notifications since there's no way to avoid the push notification from the base message. Please give us your feedback on this in #suggestions-feedback!~~ *this change was reverted*

### New Bot features:
- So many little text tweaks and aliases! Way too many to list!
- Regex Message Editing
- PK now hosts your avatars! (2/24)
- PK IDs are now 6 characters! (5/24)
- Split IDs and Capitalize IDs config options (5/24)
- Pad IDs config option (5/24)
- PK has a custom status now! (9/24)
- Poll proxying (10/24)
- `-plaintext` flag, like `-raw`, sends just the field contents but is not in a codeblock for easier mobile copying (10/24)
- Flags for more detailed switch editing and switch copying! Make a duplicate of a switch with members appended using `pk;switch copy <member1> [member2] [...]` (10/24)
  - Use the `-append` flag to add members to the end of a switch (default behavior for `pk;switch copy` but also works with `pk;switch edit`
  - Use the `-prepend` flag to add members to the beginning of a switch
  - Use the `-remove` flag to remove members from a switch
  - Use the `-first` flag to move a member to the beginning of a switch
- You can now use `-all` to add or remove all members to/from a group (10/24)
- The new system command now gives information about privacy and system recovery (10/24)
- The help command also now has info on system recovery (10/24)

### New Dashboard features:
Did you know that the dashboard is getting a full rewrite? Did you also know that PALS is looking for feedback on said rewrite? If you didn't and are interested in taking a look, head over to ⁠Beta Dashboard feedback thread﻿! Please do read the FAQ at the top of the thread beforehand, it might answer some of your questions. There's also a reaction role pinned in ⁠website﻿ for when we need additional feedback on things.
- You can now view groups as they are formatted in the bot (9/23)
- More views, filters, and sort options on the lists in group and member pages (9/23)
- You can choose which avatar (regular or proxy) shows in lists (9/23)
- More options when getting random members (2/24)
- Small text styling (10/24)

### Bug fixes:
#### Bot:
- PluralKit no longer tries to check server keepproxy in DMs (8/23)
- Allow proxying in media channel threads (10/23)
- PluralKit knows Discord's new higher per-channel webhook limit (1/24)
- While renaming a member, ID is shown if member name has spaces (1/24)
- Add link to the support server for member and group limit warnings (1/24)
- Markdown is escaped in Discord mentions (8/24)
- PK checks if user has a system registered in `pk;debug proxy` (8/24)
- Default embed color is null instead of an arbitrary gray (8/24)
- System name is no longer shown on groups cards or the group list if private (10/24)
- Group displaynames enforce a character limit again (10/24)
- You can now blacklist any channel you can proxy in (namely forums) (10/24)
- Searching correctly respects all privacy settings (10/24)

#### Dashboard:
- Public member pages no longer show up in Google search results (11/23)
- You can use the fonts Atkinson Hyperlegible and OpenDyslexia on the Dashboard again (2/24)
- Non-ASCII characters are now correctly parsed in code blocks (4/24)
- You can use any valid ID in links (hid, uuid, discord id) (6/24)

## 2023-08-25

__**New bot features:**__
- You can now set server-specific system names and icons: `pk;system servername` / `pk;system servericon`
- You can now toggle keepproxy on a per-server basis: `pk;member <name> serverkeepproxy`
- There is now a per-member toggle to turn on text-to-speech for that member's proxied messages (where server permissions allow): `pk;member <name> tts`

__**New Dashboard features:**__
- Added tiny card and text only view options for lists
- Changed color sorting: colors now sort by hue instead of hexcode value
- New "filter by" options: banner and proxy tags
- Support for the new Discord markdown! Headings and lists should now show up as they do in Discord.

__**Additional privacy settings:**__
- System name: `pk;system privacy name`
- System icons: `pk;system privacy icon`
- Member proxy tags: `pk;member <name> privacy proxy`
You can also change these new privacy settings on the Dashboard.

__Bug fixes:__

Bot:
- Reply embeds now truncate extremely long links
- Bellhop emoji (🛎️) works as a ping emoji now
- Warning now given when you set a member avatar when the member has a proxyavatar set
- PK now accepts webp images for avatars/banners
- Cleaned up command aliases for group commands

Dashboard:
- Member/group lists will not appear to load endlessly when they fail to load
- Short links will now be parsed in system name and pronouns
- Fix page freezing when filtering by privacy and editing a member's visibility
- Visiting your own public system page will now only show public info, even when logged in

## 2023-05-15
It's been a while! Here's a summary of what's new in PluralKit since the last changelog:

__**Message context commands:**__

Instead of using the❓ / ❌ / 🔔 reactions, you can now access these features from the message context menu! 

Tap-and-hold (or right-click on desktop) on a proxied message, select Apps, and these PluralKit commands will be there.

**Reactions for these commands _are not going away_!** This is just another option, for those who find reactions hard to use.

__**Proxy avatars:**__

You can now set two separate avatars for members! The new "proxy avatar" will be displayed as the avatar in proxied messages, so that you can have a "cropped" version for proxying, and still have the full uncropped image on your member card. 

You can use `pk;m <name> proxyavatar` (or `pa` as a short-hand) to update/clear the proxy avatar for a member; and there's a new field on the Dashboard for this as well.

__Smaller things + bug fixes:__

- New: You can turn off proxy error messages (such as the dreaded "PluralKit cannot proxy attachments over 25 megabytes") for your system with `pk;cfg proxy error off`.
- New: You can now clear the embeds on your own proxied messages with `pk;edit -clear-embed` (`pk;e -ce`).
- Fixed: `pk;blacklist` and `pk;log blacklist` both have better support for threads - you can blacklist individual threads now; and blacklisting a channel will properly blacklist all of the threads in that channel also.

## 2022-11-24

A lot has happened since the last changelog, so here's some of the bigger things:
 
__**Bot:**__

- You can now use `pk;random` on other systems, with `pk;system <id or @mention> random` (only if the target system has a public member list)
- There is now a `pk;config` setting to make proxy tags case insensitive - use `pk;config proxy case off` to enable this
- `pk;edit` now supports `-nospace` (`-ns`) for appending/prepending without adding a space
- The `pk;ap` embed now shows the currently latched member (when using latch autoproxy)
- The latest message in a channel can now always be reproxied
- Query IDs directly with `pk;system id`, `pk;member <member> id`, or `pk;group <group> id`

__**Dashboard:**__

- A new "card" view for members and groups
- More sorting options for the member list
- ... and some miscellaneous bugfixes :​)

## 2022-06-05

__**Official PluralKit web dashboard!**__ <https://dash.pluralkit.me>

- edit your system information on a website instead of bot commands
- easily shareable public links for your system / members / groups: <https://dash.pluralkit.me/profile>
- update your members/groups privacy settings in bulk
- save description templates which can automatically be copy-pasted into descriptions to then fill out
- filter/sort options for easier finding members in lists

**Features**
- `pk;log show` - print a list of channels where message logging is disabled
- `pk;reproxy` - re-sends an already proxied message with a different member
- automatically run `pk;export` when deleting the system
- a few added aliases / short forms for commands

**Fixes**
- don't crash on older export files
- hide discord account's server nickname from `pk;msg` if not allowed to see it
- fix flags in `pk;system color`

## 2022-04-07

- with pk;ap latch, use `\\` to stop proxying altogether instead of just for one message
- `pk;system pronouns`
- `-append` and `-prepend` flags for pk;edit - add some text to the beginning and/or end of a message, instead of replacing the entire content
- member name cards now show empty displayname field even if it is hidden (as to not leak privacy settings)
- trying to add a reaction to a paginated list before the reactions are done filling up doesn't break the list anymore
- running `pk;system @discordaccount` on an account that doesn't have a system registered now shows the correct error message
- lots of internal fixes

## 2022-02-28

mostly backend changes this month, but a couple visible changes:

- really long search queries in lists don't throw an error anymore
- if a member's name is private (and you have show private info disabled), the member's display name will show up when running commands
- API v1 now returns the correct error response code if you're trying to update data on a member that you don't own
- added documentation on what the 🔷 means in the `pk;config` command
- added `prns` and `pn` aliases to "pronouns" member command

## 2022-01-22

**Features:**
- `-with-displayname` list flag (to show the displayname in the list)
- `sa` alias for `serveravatar`

**Fixes:**
- better command help for autoproxy
- cleaned up frontpercent command, added `full` to groups as well
- cleaned up `pk;config` command parsing
- `pk;edit` command now throws errors properly
- Discord temporarily removed the raised Nitro character limit for webhooks, so it was removed on PluralKit's side as well

If you are using log cleanup, the way PluralKit finds some log messages was changed.

## 2022-01-14

**Features:**
- if you have Discord Nitro, PluralKit can now proxy messages longer than 2000 characters
- the group list now supports all the same searching / sorting flags as the member list
- there is now a "full" group list, similar to the full member list: `pk;group list full`

**Fixes:**
- `pk;group <name> random` now checks if the group is your own
- lists are now sorted correctly as to not reveal whether there is hidden information or not

## 2022-01-11

**Features:**
- log cleanup now supports ProBot
- the reply embed now shows the attachment icon if there's an embed in the original message (same as Discord's reply UI)

**Fixes:**
- `pk;edit` shows the correct error message if there is no text provided.
- `pk;debug permissions` checks if the bot is missing *Read Message History* permissions
- the group list embed shows the target system's colour instead of your own colour when looking up a different system's groups
- message info is now not deleted from the database when the associated member is deleted - looking it up shows "unknown member" but still shows the Discord user who sent the message

## 2021-12-23

**Features:**
- `pk;config` command to view / set some settings that aren't specifically related to system *information*
- config setting to hide your own private information by default when you look it up
- the member lookup order was changed - display names are now prioritized over IDs when looking up a member
- added `-id` flag to specify by-ID lookups in `pk;member` and `pk;group`
- added config setting to automatically set members/groups as private when creating

**Fixes:**
- removed "last-message" sorting in lists to fix lists being really slow for some people
- avatar URL or display name now also shows up in the short list if the relevant flag is selected
- Discord permissions for your account are now correctly applied when looking up a message with `pk;message` command
- changed `pk;system` command flow to be less confusing: any command can now be ran with (or without) a system ID target
- fixed server settings not applying sometimes
- `pk;edit` now shows an error message if the text is longer than 2000 characters

**API:**
- dispatch webhooks are here! check <https://pluralkit.me/api/dispatch> for documentation.
- data from `pk;config` is available in a new `/systems/:ref/settings` endpoint.
- BREAKING CHANGE: `timezone` field in system object was removed from API v2, and `tz` field always shows "UTC" value in API v1 (timezone is available in settings endpoint)
- some endpoints erroneously required `@me` instead of a system reference. this has been fixed

**Self-hosting**: we have updated the bot to use .NET 6. If you run a PluralKit instance and manually compile the bot, you must install the .NET 6 SDK before updating; if you use Docker, it will automatically build using .NET 6 so no change is needed

## 2021-11-07

**APIv2!**
- group endpoints! create/edit groups, edit group members
- switch endpoints! refer to a switch by an unique ID, edit switch members, edit switch timestamp, delete switch
- system/member guild endpoints! edit per-server settings, including autoproxy settings

If you're a developer, you can find the new API documentation at <https://pluralkit.me/api>.
If you use a website or other community tool that uses the API, ask the tool's developer to check out the new API features!

**Features:**
- Added `-raw` flag to `pk;message` command
- You can now use "today" to set the member's birthday to today
- Added 🔢 react to jump to a specific page in a paginated list
- Added `pk;s <id> avatar` to show a different system's avatar.
- Added a `full` argument to `pk;s frontpercent` to show the frontpercent for all time, rather than for the past month.

**Fixes:**
- Tupperbox import now correctly imports nicknames.
- You can now pass a channel ID (such as `524660142499823627`) where previously PluralKit expected a channel mention.
- `pk;log channel` now shows the currently set log channel, rather than an error message.
- `pk;log channel #channel` now throws an error if PluralKit is missing required permissions to log messages.
- Fixed error with looking up messages if the bot can't fetch user roles.
- PluralKit now respects the server's boost file size limit when re-sending attachments, instead of the global 8MB limit.

## 2021-10-03

**Switch editing!**
- `pk;sw edit <member1> [member2] ...` - Replaces the members in the current switch.
- `pk;sw edit out` - Change the current switch to a switch-out.

Also, it is now possible to provide a command response message link to `pk;msg -delete` instead of reacting with ❌ to it

**Fixes:**
- `<text>` brackets should no longer match custom emojis, mentions or channel names.
- Front histories with many members per switch should now be showing up correctly (previously, they may have been missing switches).

## 2021-09-25

**Features:**
- groups are now included in import/export!
- `pk;debug permcheck #channel` - check the permissions for one specific channel, rather than the whole server
- added `-raw` flag to servertag
- it is now possible to ❌ delete any command response with an embed **for 1 day in servers** and **forever in DMs**.
- it is now possible to edit messages in threads!

**Fixes:**
- fix markdown formatting sometimes not working in pk;edit
- running `pk;commands` doesn't show an error anymore
- separating messages from different members with the same name works again
- the error `You can only run this command on your own member.` will not happen anymore when using commands that can only target your own members anyway (switch / group commands)

## 2021-09-06

Mostly fixes this month.

**Features:**
- most commands now have a `-raw` flag; this sends the text with formatting for easier copy-pasting
- `pk;permcheck` now checks for missing "Use External Emoji" permissions

**Fixes:**
- corrected a lot of incorrect strings
- per-guild Discord account avatar is correctly used in replies
- the correct server tag is now shown on the system card
- it is now possible to delete command responses with a ❌ react in DMs and channels where PluralKit doesn't have permissions to delete messages
- `pk;unlink` doesn't prompt you to unlink the current account if you don't pass a mention

Also, refactored the code for import/export. The bot should now give way better errors when something is invalid.

## 2021-08-08

**Features:**
- Banner (large) image for systems/members/groups
- Proxy debug command: check *why* PluralKit isn't proxying your messages- `pk;debug proxy`
- Sorted / selected properties now show up in the short member list.
- You can now set a system tag for a specific server, or disable it in a specific server: `pk;system servertag`

**Fixes:**
- The full image is now shown in cards for member avatars, instead of the resized image used for proxying.
- Messages are now editable/deletable by any account linked to a system, not just the account that the message was sent from.
- Messages sent by two members with the same name shouldn't get merged anymore.

## 2021-07-27

**Features:**
- Added "flat" front percent view (percentages add up to 100% even when co-fronting)
- Added log cleanup for Vortex
- Editing messages from DMs now gives a ✅ confirmation reaction
- Discord released threads! PK should work in threads when they roll out to your server.

**Fixes:**
- Front percent now shows the correct percentages when removing "no fronter" item
- Setting proxy tags now correctly checks if any other members have the same proxy tags
- Messages originally longer than 2000 characters are not cut off (as bots/webhooks can't send messages longer than 2000 characters, an error is thrown instead)
- Events in announcement channels are now handled correctly.
- Miscellaneous internal stability fixes.

Bonus: we've switched yes/no confirmations to using *Discord buttons* instead of reactions. (If you're not able to click or tap on the reaction, you can reply with "yes" or "no" by text.)

## 2021-05-17

Just a quick moderation-related change: message edits are now logged in PluralKit's log.

Also, fixed some errors related to editing messages in DMs

## 2021-05-07

**Message editing!**
- Type `pk;edit <text>` to edit your last proxied message.
- Reply to any message with the same command to edit it.
- Use a message link (ie. `pk;edit <link> <text>`) to edit a message in another channel or from DMs.

**Other changes**
- Added system color is now in the frontpercent embed
- Added `-raw` flag for looking up display names printing
- Added server ID argument to `pk;system proxy

## 2021-05-03

- `pk;member new` now lets you attach an avatar along with the command and it'll set it immediately
- Proxy reply embed now shows the member color
- When setting proxy tags, you can now use the word `text` in uppercase as well

## 2021-04-29

Features:
- added looking up the raw mention of a message author with `pk;msg <id|link> author`
- added deleting a message with `pk;msg <id|link> delete`, as to not require reaction permissions to delete messages
- added colours to systems and groups
- added frontpercent for groups with `pk;group <group> frontpercent`
- added a `-fronters-only` or `-fo` flag to hide "no fronter" on the frontpercent cards

Bugfixes:
- fixed importing pronouns and message count
- fixed looking up messages with a discord canary link (and then fixed looking up normal links >.<)
- fixed a few "internal error" messages and other miscellaneous bugs
(also, `pk;member <name> soulscream` is a semi-secret command for the time being, if you know what this means, have fun :3 🍬)

## 2020-12-20

**Inline Replies**: We've received (semi-)final confirmation that proper webhook replies will not be supported by Discord. As an alternative, we've implemented a workaround: proxied replies will now have a small embed attached containing information about the message that was replied to, and a link you can click to jump to the original. I know it's not quite the same, but it's better than nothing :​)

The "design" of this is still subject to change, if necessary - do bring up suggestions or feedback in #suggestions-feedback 🙂

## 2020-12-10

Autoproxy improvements:
- Added per-account autoproxy toggle: `pk;autoproxy account on/off`
- Added per-member autoproxy toggle: `pk;member <name> autoproxy on/off`
- Added configurable latch timeout: `pk;autoproxy timeout <hours>`

Group improvements:
- Added alternative syntax for adding members to groups: `pk;member <member> group add/remove <group1> [group2] [group3...]`
- Added `pk;random -group`

Other changes:
- Added LogClean support for GiselleBot
- Added dark theme to the website
- Fixed proxying single-character names
- Fixed proxying in converted announcements channel
- General stability improvements (lol)

## 2020-11-15

Changes:
- Added looking up groups by their display name
- Made linked accounts display on separate lines
- **For server admins:** Added better handling of "chat filter" bots - if the filter bots delete a proxy trigger message before PK does, PK will delete the proxy message as well

Bugfixes:
- Fixed transparency on avatars added by attachment (you'll need to reset the avatar for this to apply)
- Fixed log channel permission check looking at the *chat* channel rather than the *log* channel
- Fixed proxying messages where the word "Clyde" appears multiple times in the name
- Fixed prompt message when clearing server avatar
- Fixed formatting escapes in the system group list

## 2020-10-23

New features:
- Confirmation when `-clear`-ing most properties
- Deleting embed responses from the bot using ❌ (works up to 2 hours after the message was first sent)
- `pk;export` sends a separate message with the file link for easy copying
- Creating a member reminds you to make it private if you have any private members already

Other stuff:
- The command list in the documentation has been updated with the group commands
- The member created message now has a valid link to the relevant documentation page
- Fixed importing systems with large members when you've had the member limit raised

## 2020-10-09
- Added per-system member limit overrides - if anyone needs the limit increased, let me know in #bot-support !
- Default member limit has been decreased from 1500 (back) to 1000, but anyone currently above 1000 members has a limit of 2000 members set 🙂

## 2020-09-21
- Fixed Tupperbox importing, for real this time (I hope)
- Redesigned the internal error message, now with 50% less API bans (sorry about that~)

## 2020-08-28

Tiny update so no ping but: emojis from Unicode 11 and 12 (added after 2017) now sort the same as the other ones 🙂

## 2020-08-25

Apologies for the long span of time without updates (although we did get member privacy in! I just forgot to announce!), but:
You can now sort your system members into **groups**! Each member's groups will show up on their member card, you can list all members of a group, etc.

The following commands should get you started:
```
pk;group new <name>
pk;group <name> add/remove <members> (multiple members work)
pk;group <name> description/icon/privacy (same as member/system commands)
pk;group <name> list
pk;group list (lists all groups, equivalent to pk;system system> list)
```

Have fun 🙂

## 2020-06-13
Added toggling reaction pings ( 🏓  / ❗  / 🔔 ) per-system: `pk;system ping disable`

## 2020-06-07
Added sort/filter support to `pk;system list` and similar commands. This is undocumented at the moment (doing a docs rewrite soonish), but:
- The following flags change the sort order of the list: `-by-display-name`, `-by-id`, `-by-message-count`, `-by-created`, `-by-last-fronted`, `-by-last-message` ,`-by-birthday` (these should hopefully be self-explanatory)
- The flag `-reverse` (or `-rev`, or `-r`) will reverse the sort order
- The flag `-search-description` will cause `pk;find` to also search in member descriptions
- The flag `-private-only` will only show private members
- The flag `-all` will show all members (default visibility is "public only")
- **For the full list only** (ie. `pk;system list full`): the flags `-with-last-switch`, `-with-last-message`, and `-with-message-count` will show the last switch/message timestamp and/or the message count on each member
- The commands `pk;system find` and `pk;system list` are now merged, and both support search terms at the end of the command 🙂

## 2020-05-05
- You can now respond to emoji reaction prompts ("are you sure you want to \<xyz\>") with "yes", "no", "y" or "n" in chat. Hopefully this is useful for people who struggle with the reactions for various reasons, or just people who don't feel like waiting for them to... slowly... pop up. :)
- Like, a trillion different stability and bug fixes I can't even list, but that aren't super useful to know about anyway (migrated to a completrely different Discord library, for one).

## 2020-03-04
- Restructured setter commands to *show* information by default, with a `-clear` flag to clear
- Added support for Vanessa to `pk;logclean`
- Added missing commands to the in-bot command lists
- Fixed Tupperbox importing of birthdays

## 2020-02-21
- Fixed card description line breaks on iOS
- Fixed proxy tag commands when the tag starts with a `-`

## 2020-02-15
- Member lookups by ❓ now sends a member card too
- Added `pk;logclean` to clean up deleted message logs from other logs

## 2020-02-13
- Added an alias for `pk;s list`: `pk;list` (or even `pk;l`)
- Added a command to *search* system members by name: `pk;find <search term>`

**Note:** I also rolled out some changes to command parsing to allow future support for flags.

*bonus!* Added server-specific member avatars! (`pk;member <name> serveravatar`)

## 2020-02-12
- Setting February 29th as a birthday with no year now works
- Editing the *last message in a channel* to contain proxy tags now triggers a proxy
- `pk;member <name> public` and `pk;member <name> private` now sets to public and private, respectively, without toggling. `pk;member <name> privacy` still toggles and can take a `on` or `off` argument too.

## 2020-01-18
Front history is now paginated

## 2020-01-25
Added autoproxying (`pk;autoproxy`)

## 2019-12-28
Added avatar previewing (`pk;m <member> avatar`)

## 2019-12-27
- Added per-server display names (`pk;m <member> servername <server name>`)
- Fixed image-only proxying when the tags contain spaces
- Fixed reactions to yes/no prompts sometimes not registering

## 2019-12-22
- Added support for multiple attachments in proxied messages
- Added a warning message when setting proxy tags conflicting with other members
- Optimized bot connection, memory usage and member card lookup (should prevent peak time crashes)
- Added pinging users by reaction ( 🔔 / ❗ / 🏓 )
- Added toggling proxying per-server (`pk;system proxy`)

## 2019-11-12
Added a per-channel proxy blacklist: `pk;blacklist add/remove #channel1 #channel2 ...`

Added a per-channel *logging* blacklist: `pk;log enable/disable #channel1 #channel2...`

## 2019-11-03
Added a few command listing commands, eg. `pk;system help`, `pk;member help`, `pk;switch help`, and so on.
Also updated formatting and added command descriptions to "command not found, try these" errors.


#### addendum:

The export data file format has changed slightly:
- The member fields `prefix` and `suffix` have been removed.
- The field `proxy_tags` has been added, and is an array of objects, each object containing a `prefix` and `suffix` field.
- The boolean field `keep_proxy` has been added.

## 2019-10-31
Added the ability to keep the proxy tags in the proxied message. See `pk;member <name> keepproxy`.

## 2019-10-30
Added multiple proxy tags support. See `pk;member <name> proxy add <tags>` and such.

## 2019-10-27
- Command parsing has been rewritten, leading to several minor usability changes (including better syntax errors)
- A member cap has been instated. Each system can now have up to 1000 members. The system with the most members has at time of writing around 650, so we don't anticipate this limit being hit often. It can always be raised if need be.
- Importing and exporting has been made a lot faster and more robust
- System and member cards now display the creation date of the system/member in the footer
- When querying a message with the ❓ emoji, the account who sent the message's server roles will be displayed (useful for, say, pronouns)

## 2019-08-14
In accordance with Discord's API changes, the limit for proxied member name length has been upped to 80 characters.

## 2019-08-09
Display names have been added! See the docs 😃

## 2019-07-26
There's a new command for checking whether your server's permission setup will work for proxying. Just use `pk;permcheck` in the server, or `pk;permcheck <ServerID>` in DMs.

## 2019-07-15
- The bot's been rewritten in C#! Everything should function mostly the same, but there are a few notable differences:
- The API has been given a `/v1/` URL prefix for versioning purposes
- The endpoint for logging new switches has had a breaking change (now takes a JSON document)
- The syntax for accepting relative switch move timestamps has changed a bit, now in a shorthand format (eg. `5d12h` or `5m30s`)
- The `pk;system/member avatar` commands now check the file size and image dimensions of the image you give it
- There's a shiny new website with documentation! <https://pluralkit.me/>
- Probably more (that I forgot)!

## 2019-05-11
- The bot's icon has been changed to our new fancy mascot (finally!), by @Layl#8888. 
- The system card now displays the amount of members in the system

## 2019-04-25
API now takes the token in the `Authorization` header rather than `X-Token`

## 2019-04-14
- Reacting with ❓ now shows member pronouns if applicable
- Message cards (by pk;message or reaction) now show a clickable account tag for quick access

## 2019-03-30
- You can now react with ❓ (or ❔) to a proxied message to get information about its sender DM'd to you.
- Help has been revamped (again), and you can now see a full command list with `pk;commands`, as well as individual command information (eg. `pk;help system avatar`).

## 2019-03-08
The member list has been moved to a separate command: `pk;system list`. This properly sorts the members alphabetically, and paginates in systems with more than 10 members. You can also check another system's member list: `pk;system @Someone list`, etc.

## 2019-02-28
Time zone searching is now way more accurate, and doesn't require you to hunt for the proper time zone name. Just type in a city near you and it should just work.¹

For example: `pk;system timezone Wichita Falls`, or `pk;system timezone Fucking` (the one in Austria).

¹ Pretty much any city works. Even like, really obscure ones. Hopefully.

## 2019-02-26
PluralKit's proxying speed has been nerfed - this should hopefully prevent most instances of "stuck messages", where both the original message and the proxied message will stick.

## 2019-02-16
- Added Tupperbox importing from files. This is faster and less error-prone.
- Added smart quote support when quoting parameters. This allows you to use quotes on devices that add smart quotes (eg. Apple products)
- Added message count to the export file
- Added a license to the project - we are now under Apache 2.
- Added more aggressive notifying for permission errors (now falls back to DMs)

## 2018-12-18
Added system time zones. This means that you can declare a per-system time zone that'll be used every time you run a PluralKit command from an account linked to that system. It defaults to UTC as always, but this means you can now have front history, switch movement, et cetera displayed in your local time zone. Use `pk;system timezone [timezone]` to set this.

For example: `pk;system timezone Berlin`, or `pk;system timezone GMT+4`.
