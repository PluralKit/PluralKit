---
layout: default
title: User Guide
permalink: /guide
description: PluralKit's user guide contains a walkthrough of the bot's features, as well as how to use them.
nav_order: 2
---

# User Guide
{: .no_toc }

## Table of Contents
{: .no_toc .text-delta }

1. TOC
{:toc}

## Adding the bot to your server
If you want to use PluralKit on a Discord server, you must first *add* it to the server in question. For this, you'll need the *Manage Server* permission on there.

Use this link to add the bot to your server:

[https://discord.com/oauth2/authorize?client_id=466378653216014359&scope=bot&permissions=536995904](https://discord.com/oauth2/authorize?client_id=466378653216014359&scope=bot&permissions=536995904)

Once you go through the wizard, the bot account will automatically join the server you've chosen. Please ensure the bot has the *Read Messages*, *Send Messages*, *Manage Messages*, *Attach Files* and *Manage Webhooks* permission in the channels you want it to work in. 

## System management
In order to do most things with the PluralKit bot, you'll need to have a system registered with it. A *system* is a collection of *system members* that may be used by one or more *Discord accounts*.

### Creating a system
If you do not already have a system registered, use the following command to create one:

    pk;system new
   
Optionally, you can attach a *system name*, which will be displayed in various information cards, like so:

    pk;system new My System Name
    
### Viewing information about a system
To view information about your own system, simply type:

    pk;system
    
To view information about *a different* system, there are a number of ways to do so. You can either look up a system by @mention, by account ID, or by system ID. For example:

    pk;system @Craig#5432
    pk;system 466378653216014359
    pk;system abcde
    
### System description
If you'd like to add a small blurb to your system information card, you can add a *system description*. To do so, use the `pk;system description` command, as follows:

    pk;system description This is my system description. Hello. Lorem ipsum dolor sit amet.
    
There's a 1000 character length limit on your system description - which is quite a lot! 

If you'd like to remove your system description, just type `pk;system description` without any further parameters.

### System avatars
If you'd like your system to have an associated "system avatar", displayed on your system information card, you can add a system avatar. To do so, use the `pk;system avatar` command. You can either supply it with an direct URL to an image, or attach an image directly. For example.

    pk;system avatar http://placebeard.it/512.jpg
    pk;system avatar [with attached image]
    
To clear your avatar, simply type `pk;system avatar` with no attachment or link.

### System tags
Your system tag is a little snippet of text that'll be added to the end of all proxied messages.
For example, if you want to proxy a member named `James`, and your system tag is `| The Boys`, the final name displayed
will be `James | The Boys`. This is useful for identifying your system in-chat, and some servers may require you use
a system tag. Note that emojis *are* supported! To set one, use the `pk;system tag` command, like so:

    pk;system tag | The Boys
    pk;system tag (Test System)
    pk;system tag 🛰️
    
If you want to remove your system tag, just type `pk;system tag` with no extra parameters.

**NB:** When proxying, the *total webhook username* must be 32 characters or below. As such, if you have a long system name, your tag might be enough
to bump it over that limit. PluralKit will warn you if you have a member name/tag combination that will bring the combined username above the limit.
You can either make the member name or the system tag shorter to solve this. 
    
### Adding or removing Discord accounts to the system
If you have multiple Discord accounts you want to use the same system on, you don't need to create multiple systems.
Instead, you can *link* the same system to multiple accounts.

Let's assume the account you want to link to is called @Craig#5432. You'd link it to your *current* system by running this command from an account that already has access to the system:

    pk;link @Craig#5432
    
PluralKit will require you to confirm the link by clicking on a reaction *from the other account*. 

If you now want to unlink that account, use the following command:

    pk;unlink @Craig#5432
    
You may not remove the only account linked to a system, as that would leave the system inaccessible. Both the `pk;link` and `pk;unlink` commands work with account IDs instead of @mentions, too.

### Setting a system time zone
PluralKit defaults to showing dates and times in [UTC](https://en.wikipedia.org/wiki/Coordinated_Universal_Time). 
If you'd like, you can set a *system time zone*, and as such every date and time displayed in PluralKit
(on behalf of your system) will be in the system time zone. To do so, use the `pk;system timezone` command, like so:

    pk;system timezone Europe/Copenhagen
    pk;system timezone America/New_York
    pk;system timezone DE
    pk;system timezone 🇬🇧
    
You can specify time zones in various ways. In regions with large amounts of time zones (eg. the Americas, Europe, etc),
specifying an exact time zone code is the best way. To get your local time zone code, visit [this site](https://xske.github.io/tz).
You can see the full list [here, on Wikipedia](https://en.wikipedia.org/wiki/List_of_tz_database_time_zones) (see the column *TZ database name*).
You can also search by country code, either by giving the two-character [*ISO-3166-1 alpha-2* country code](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2#Officially_assigned_code_elements) (eg. `GB` or `DE`), or just by a country flag emoji.

To clear a time zone, type `pk;system timezone` without any parameters. 

### Deleting a system
If you want to delete your own system, simply use the command:

    pk;system delete
    
You will need to verify by typing the system's ID when the bot prompts you to - to prevent accidental deletions.

## Member management

In order to do most things related to PluralKit, you need to work with *system members*.

Most member commands follow the format of `pk;member MemberName verb Parameter`. Note that if a member's name has multiple words, you'll need to enclose it in "double quotes" throughout the commands below.

### Creating a member
You can't do much with PluralKit without having registered members with your system, but doing so is quite simple - just use the `pk;member new` command followed by the member's name, like so:

    pk;member new John
    pk;member new Craig Smith
    
As the one exception to the rule above, if the name consists of multiple words you must *not* enclose it in double quotes.

### Looking up member info
To view information about a member, there are a couple ways to do it. Either you can address a member by their name (if they're in your own system), or by their 5-character *member ID*, like so:

    pk;member John
    pk;member qazws

Member IDs are the only way to address a member in another system, and you can find it in various places - for example the system's member list, or on a message info card gotten by reacting to messages with a question mark.

### Listing system members
To list all the members in a system, use the `pk;system list` command. This will show a paginated list of all member names in the system. You can either run it on your own system, or another - like so:

    pk;system list
    pk;system @Craig#5432 list
    pk;system qazws list
    
If you want a more detailed list, with fields such as pronouns and description, add the word `full` to the end of the command, like so:

    pk;system list full
    pk;system @Craig#5432 list full
    pk;system qazws list full

### Member renaming
If you want to change the name of a member, you can use the `pk;member rename` command, like so:

    pk;member John rename Joanne
    pk;member "Craig Smith" rename "Craig Johnson"
    
### Member display names
Normally, when proxying a member, the name displayed in the proxied message will be the member's name. However, in some cases
you may want to display a different name. For example, you may want to include a member's pronouns inside the proxied name,
indicate a subsystem, include emojis or symbols that don't play nice with the command syntax, or just in general show a different name from the member's "canonical" name.

In such cases you can set the member's *display name*. Which will, well, display that name instead. You can set
a display name using the `pk;member displayname` command, like so:

    pk;member John displayname Jonathan
    pk;member Robert displayname Bob (he/him)
    
To remove a display name, just use the same command with no last parameter, eg:

    pk;member John displayname
    
This will remove the display name, and thus the member will be proxied with their canonical name.

### Member server display names
If you'd like to set a display name (as above), but only for a specific server, you can set the member's *server display name*.
This functions just like global display names, but only in the same server you set them in. For example:

    pk;member John servername AdminJohn
    
The server name applies to the same server you run the command in, so naturally this command doesn't function in DMs (as you cannot proxy in DMs).
    
### Member description
In the same way as a system can have a description, so can a member. You can set a description using the `pk;member description` command, like so:

    pk;member John description John is a very cool person, and you should give him hugs.
    
As with system descriptions, the member description has a 1000 character length limit. 
To clear a member description, use the command with no additional parameters (eg. `pk;member John description`).

### Member color
A system member can have an associated color value.
This color is *not* displayed as a name color on proxied messages due to a Discord limitation,
but it's shown in member cards, and it can be used in third-party apps, too.
To set a member color, use the `pk;member color` command with [a hexadecimal color code](https://htmlcolorcodes.com/), like so:

    pk;member John color #ff0000
    pk;member John color #87ceeb
    
To clear a member color, use the command with no color code argument (eg. `pk;member John color`).

### Member avatar
If you want your member to have an associated avatar to display on the member information card and on proxied messages, you can set the member avatar. To do so, use the `pk;member avatar` command. You can either supply it with an direct URL to an image, or attach an image directly. For example.

    pk;member John avatar http://placebeard.it/512.jpg
    pk;member "Craig Johnson" avatar   (with an attached image)
    
To preview the current avatar (if one is set), use the command with no arguments:

    pk;member John avatar
    
To clear your avatar, use the subcommand `avatar clear` (eg. `pk;member John avatar clear`).

### Member server avatar
You can also set an avatar for a specific server. This will "override" the normal avatar, and will be used when proxying messages and looking up member cards in that server. To do so, use the `pk;member serveravatar` command, in the same way as the normal avatar command above:

    pk;member John serveravatar
    pk;member John serveravatar http://placebeard.it/512.jpg
    pk;member "Craig Johnson" serveravatar   (with an attached image)
    pk;member John serveravatar clear

### Member pronouns
If you want to list a member's preferred pronouns, you can use the pronouns field on a member profile. This is a free text field, so you can put whatever you'd like in there (with a 100 character limit), like so:

    pk;member John pronouns he/him
    pk;member "Craig Johnson" pronouns anything goes, really
    pk;member Skyler pronouns xe/xir or they/them

To remove a member's pronouns, use the command with no pronoun argument (eg. `pk;member John pronouns`).

### Member birthdate 
If you want to list a member's birthdate on their information card, you can set their birthdate through PluralKit using the `pk;member birthdate` command. Please use [ISO-8601 format](https://xkcd.com/1179/) (`YYYY-MM-DD`) for best results, like so:

    pk;member John birthdate 1996-07-24
    pk;member "Craig Johnson" birthdate 2004-02-28
    
You can also set a birthdate without a year, either in `MM-DD` format or `Month Day` format, like so:

    pk;member John birthdate 07-24
    pk;member "Craig Johnson" birthdate Feb 28
    
To clear a birthdate, use the command with no birthday argument (eg. `pk;member John birthdate`).

### Deleting members
If you want to delete a member, use the `pk;member delete` command, like so:

    pk;member John delete
    
You'll need to confirm the deletion by replying with the member's ID when the bot asks you to - this is to avoid accidental deletion.

## Proxying
Proxying is probably the most important part of PluralKit. This allows you to essentially speak "as" the member,
with the proper name and avatar displayed on the message. To do so, you must at least [have created a member](#creating-a-system).

### Setting up proxy tags
You'll need to register a set of *proxy tags*, which are prefixes and/or suffixes you "enclose" the real message in, as a signal to PluralKit to indicate
which member to proxy as. Common proxy tags include `[square brackets]`, `{curly braces}` or `A:letter prefixes`.

To set a proxy tag, use the `pk;member proxy` command on the member in question. You'll need to provide a "proxy example", containing the word `text`.
For example, if you want square brackets, the proxy example must be `[text]`. If you want a letter prefix, make it something like `A:text`. For example:

    pk;member John proxy [text]
    pk;member "Craig Johnson" proxy {text}
    pk;member John proxy J:text
    
You can have any proxy tags you want, including one containing emojis.

You can now type a message enclosed in your proxy tags, and it'll be deleted by PluralKit and reposted with the appropriate member name and avatar (if set).

**NB:** If you want `<angle brackets>` as proxy tags, there is currently a bug where custom server emojis will (wrongly)
be interpreted as proxying with that member (see [issue #37](https://github.com/xSke/PluralKit/issues/37)). The current workaround is to use different proxy tags.

### Using multiple distinct proxy tag pairs
If you'd like to proxy a member in multiple ways (for example, a name or a nickname, uppercase and lowercase variants, etc), you can add multiple tag pairs.
When proxying, you may then use any of the tags to proxy for that specific member.

To add a proxy tag to a member, use the `pk;member proxy add` command:
    pk;member John proxy add {text}
    pk;member Craig proxy add C:text
    
To remove a proxy tag from a member, use the `pk;member proxy remove` command:
    pk;member John proxy remove {text}
    pk;member Craig proxy remove C:text

### Keeping your proxy tags
If you'd like your proxied messages to include the proxy tags, you can enable the "keep proxy tags" option for a given member, like so:

    pk;member John keepproxy on

Turning the option off is similar - replace "on" with "off" in the command. The default value for every member is off. When proxying
a member with multiple proxy tags, the proxy tag used to trigger a given proxy will be included.

The practical effect of this is:
* **Keep proxy tags on:** `[Message goes here]` -> [Message goes here]
* **Keep proxy tags off:** `[Message goes here]` -> Message goes here 

### Querying message information
If you want information about a proxied message (eg. for moderation reasons), you can query the message for its sender account, system, member, etc.

Either you can react to the message itself with the ❔ or ❓ emoji, which will DM you information about the message in question, 
or you can use the `pk;message` command followed by [the message's ID](https://support.discord.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID-).

### Pinging a specific user
If you'd like to "ping" the account behind a proxied message without having to query the message and ping them yourself,
you can react to the message with the 🔔 or ❗ emoji, and PluralKit will ping the relevant member and account in the same
channel on your behalf with a link to the message you reacted to.

### Disabling proxying on a per-server basis
If you need to disable proxying messages for your system entirely in a specific server (for example, if you'd like to
use a different proxy bot there), you can type `pk;system proxy on/off` to do that.

### Deleting messages
Since the messages will be posted by PluralKit's webhook, there's no way to delete the message as you would a normal user message.
To delete a PluralKit-proxied message, you can react to it with the ❌ emoji. Note that this only works if the message has
been sent from your own account.

### Autoproxying
The bot's *autoproxy* feature allows you to have messages be proxied without directly including the proxy tags. Autoproxy can be set up in various ways. There are three autoproxy modes currently implemented:

To see your system's current autoproxy settings, simply use the command:
    pk;autoproxy
    
To disable autoproxying for the current server, use the command:
    pk;autoproxy off
    
*(hint: `pk;autoproxy` can be shortened to `pk;ap` in all related commands)*

#### Front mode
This autoproxy mode will proxy messages as the current *first* fronter of the system. If you register a switch with `Alice` and `Bob`, messages without proxy tags will be autoproxied as `Alice`.
To enable front-mode autoproxying for a given server, use the following command:

    pk;autoproxy front
    
#### Latch mode
This autoproxy mode will essentially "continue" previous proxy tags. If you proxy a message with `Alice`'s proxy tags, messages posted afterwards will be proxied as Alice. Proxying again with someone else's proxy tags, say, `Bob`, will cause messages *from then on* to be proxied as Bob.
In other words, it means proxy tags become "sticky". This will carry over across all channels in the same server.

To enable latch-mode autoproxying for a given server, use the following command:

    pk;autoproxy latch
    
#### Member mode 
This autoproxy mode will autoproxy for a specific selected member, irrelevant of past proxies or fronters.

To enable member-mode autoproxying for a given server, use the following command, where `<member>` is a member name (in "quotes" if multiple words) or 5-letter ID:

    pk;autoproxy <member>

## Managing switches
PluralKit allows you to log member switches through the bot.
Essentially, this means you can mark one or more members as *the current fronter(s)* for the duration until the next switch.
You can then view the list of switches and fronters over time, and get statistics over which members have fronted for how long.

### Logging switches
To log a switch, use the `pk;switch` command with one or more members. For example:

    pk;switch John
    pk;switch "Craig Johnson" John
    
Note that the order of members are preserved (this is useful for indicating who's "more" at front, if applicable).
If you want to specify a member with multiple words in their name, remember to encase the name in "double quotes".

### Switching out
If you want to log a switch with *no* members, you can log a switch-out as follows:

    pk;switch out

### Moving switches
If you want to log a switch that happened further back in time, you can log a switch and then *move* it back in time, using the `pk;switch move` command.
You can either specify a time either in relative terms (X days/hours/minutes/seconds ago) or in absolute terms (this date, at this time).
Absolute times will be interpreted in the [system time zone](#setting-a-system-time-zone). For example:

    pk;switch move 1h
    pk;switch move 4d12h
    pk;switch move 2 PM
    pk;switch move May 8th 4:30 PM

Note that you can't move a switch *before* the *previous switch*, to avoid breaking consistency. Here's a rough ASCII-art illustration of this:

           YOU CAN NOT               YOU CAN
            MOVE HERE               MOVE HERE       CURRENT SWITCH                 
                v                       v               START                   NOW
    [===========================]       |                 v                      v
                                 [=== PREVIOUS SWITCH ===]|                      |
                                 \________________________[=== CURRENT SWITCH ===]
                                                          
    ----- TIME AXIS ---->

### Delete switches
If you'd like to delete the most recent switch, use the `pk;switch delete` command. You'll need to confirm
the deletion by clicking a reaction.

If you'd like to clear your system's entire switch history, use the `pk;switch delete all` command. This isn't reversible!

### Querying fronter
To see the current fronter in a system, use the `pk;system fronter` command. You can use this on your current system, or on other systems. For example:

    pk;system fronter
    pk;system @Craig#5432 fronter
    pk;system qazws fronter

### Querying front history
To look at the front history of a system (currently limited to the last 10 switches). use the `pk;system fronthistory` command, for example:

    pk;system fronthistory
    pk;system @Craig#5432 fronthistory
    pk;system qazws fronthistory
    
### Querying front percentage
To look at the per-member breakdown of the front over a given time period, use the `pk;system frontpercent` command. If you don't provide a time period, it'll default to 30 days. For example:

    pk;system frontpercent
    pk;system @Craig#5432 frontpercent 7d
    pk;system qazws frontpercent 100d12h

Note that in cases of switches with multiple members, each involved member will have the full length of the switch counted towards it. This means that the percentages may add up to over 100%.

## Privacy
There are various reasons you may not want information about your system or your members to be public. As such, there are a few controls to manage which information is publicly accessible or not.

### System privacy
At the moment, there are four aspects of system privacy that can be configured.

- System description
- Current fronter
- Front history
- Member list

Each of these can be set to **public** or **private**. When set to **public**, anyone who queries your system (by account or system ID, or through the API), will see this information. When set to **private**, the information will only be shown when *you yourself* query the information with the -all flag, for example `pk;system -all`. The cards will still be displayed in the channel the commands are run in, so it's still your responsibility not to pull up information in servers where you don't want it displayed.

To update your system privacy settings, use the following commands:

    pk;system privacy <subject> <level>
    
where `<subject>` is either `description`, `fronter`, `fronthistory` or `list`, corresponding to the options above, and `<level>` is either `public` or `private`. `<subject>` can also be `all` in order to change all subjects at once.

For example:

    pk;system privacy description private
    pk;system privacy fronthistory public
    pk;system privacy list private

When the **member list** is **private**, other users will not be able to view the full member list of your system, but they can still query individual members given their 5-letter ID. If **current fronter** is private, but **front history** isn't, someone can still see the current fronter by looking at the history (this combination doesn't make much sense).

### Member privacy
There are also seven options for configuring member privacy;

- Name
- Description
- Avatar
- Birthday
- Pronouns
- Metadata *(message count, creation date, etc)*
- Visibility *(whether the member shows up in member lists)*

As with system privacy, each can be set to **public** or **private**. The same rules apply for how they are shown, too. When set to **public**, anyone who queries your system (by account or system ID, or through the API), will see this information. When set to **private**, the information will only be shown when *you yourself* query the information with the -all flag, for example `pk;member <member> -all`. The cards will still be displayed in the channel the commands are run in, so it's still your responsibility not to pull up information in servers where you don't want it displayed.

However, there are two catches:
- When the **name** is set to private, it will be replaced by the member's **display name**, but only if they have one! If the member has no display name, **name privacy will not do anything**. PluralKit still needs some way to refer to a member by name :) 
- When **visibility** is set to private, the member will not show up in member lists unless `-all` is used in the command (and you are part of the system).

To update a members privacy you can use the command:

    member <member> privacy <subject> <level>

where `<member>` is the name or the id of a member in your system, `<subject>` is either `name`, `description`, `avatar`, `birthday`, `pronouns`, `metadata`, or `visiblity` corresponding to the options above, and `<level>` is either `public` or `private`. `<subject>` can also be `all` in order to change all subjects at once.  
`metadata` will affect the message count, the date created, the last fronted, and the last message information.

For example:

    pk;member John privacy visibility private
    pk;member "Craig Johnson" privacy description public
    pk;member Robert privacy birthday public
    pk;member Skyler privacy all private

## Moderation commands

### Log channel
If you want to log every proxied message to a separate channel for moderation purposes, you can use the `pk;log` command with the channel name.
This requires you to have the *Manage Server* permission on the server. For example:

    pk;log #proxy-log
    
To disable logging, use the `pk;log` command with no channel name.

### Channel blacklisting
It's possible to blacklist a channel from being used for proxying. To do so, use the `pk;blacklist` command, for examplle:

    pk;blacklist add #admin-channel #mod-channel #welcome
    pk;blacklist add all
    pk;blacklist remove #general-two
    pk;blacklist remove all
    
This requires you to have the *Manage Server* permission on the server. 

### Log cleanup
Many servers use *logger bots* for keeping track of edited and deleted messages, nickname changes, and other server events. Because
PluralKit deletes messages as part of proxying, this can often clutter up these logs. To remedy this, PluralKit can delete those
log messages from the logger bots. To enable this, use the following command:

    pk;logclean on
    
This requires you to have the *Manage Server* permission on the server. At the moment, log cleanup works with the following bots:
- Auttaja 
- blargbot
- Carl-bot
- Circle
- Dyno
- GenericBot
- Logger (#6088 and #6278)
- Mantaro
- Pancake
- UnbelievaBoat

If you want support for another logging bot, [let me know on the support server](https://discord.gg/PczBt78).

Another alternative is to use the **Gabby Gums** logging bot - an invite link for which can be found [on Gabby Gums' support server](https://discord.gg/Xwhk89T).

## Importing and exporting data
If you're a user of another proxy bot (eg. Tupperbox), or you want to import a saved system backup, you can use the importing and exporting commands.

### Importing from Tupperbox
If you're a user of the *other proxying bot* Tupperbox, you can import system and member information from there. This is a fairly simple process:

1. Export your data from Tupperbox:
```
tul!export
```
2. Copy the URL for the data file (or download it)
3. Import your data into PluralKit:
```
pk;import https://link/to/the/data/file.json
```
*(alternatively, run `pk;import` by itself and attach the .json file)*

Note that while Tupperbox supports features such as multiple proxies per member, per-member system tags, and member groups, PluralKit does not.
PluralKit will warn you when you're importing a Tupperbox file that makes use of such features, as they will not carry over. 

### Importing from PluralKit
If you have an exported file from PluralKit, you can import system, member and switch information from there like so:
1. Export your data from PluralKit:
```
pk;export
```
2. Copy the URL for the data file (or download it)
3. Import your data into PluralKit:
```
pk;import https://link/to/the/data/file.json
```
*(alternatively, run `pk;import` by itself and attach the .json file)*

### Exporting your PluralKit data
To export all the data associated with your system, run the `pk;export` command. This will send you a JSON file containing your system, member, and switch information.