---
title: User Guide
description: PluralKit's user guide contains a walkthrough of the bot's features, as well as how to use them.
permalink: /guide

# To prevent sidebar from getting super long
sidebarDepth: 1
---

# User Guide

## Adding the bot to your server
If you want to use PluralKit on a Discord server, you must first *add* it to the server in question. For this, you'll need the *Manage Server* permission on there.

Use this link to add the bot to your server:

[https://discord.com/oauth2/authorize?client_id=466378653216014359&scope=bot&permissions=536995904](https://discord.com/oauth2/authorize?client_id=466378653216014359&scope=bot&permissions=536995904)

Once you go through the wizard, the bot account will automatically join the server you've chosen. Please ensure the bot has the *Read Messages*, *Send Messages*, *Manage Messages*, *Attach Files* and *Manage Webhooks* permission in the channels you want it to work in. 

*ℹ️ You can have a space after `pk;`; e.g. `pk;system` and `pk; system` will do the same thing.*

## System management
In order to do most things with the PluralKit bot, you'll need to have a system registered with it. A *system* is a collection of *system members* that may be used by one or more *Discord accounts*.

These examples are for the user `@Craig#5432`, who has headmates Alice, Craig Johnson (formerly Craig Smith), Joanne (formerly John, aka Jo), Skyler (aka Sky), and Unknown.

### Creating a system
If you do not already have a system registered, use the following command to create one:

    pk;system new
   
Optionally, you can attach a *system name*, which will be displayed in various information cards, like so:

    pk;system new My System Name

*ℹ️ You can also use `pk;s` as shorthand for `pk;system`.*

### Viewing information about a system
To view information about your own system, simply type:

    pk;system

To view information about *a different* system, there are a number of ways to do so. You can either look up a system by @mention, by account ID, or by system ID. For example:

    pk;system @Craig#5432
    pk;s 466378653216014359
    pk;s abcde
    
### System description
If you'd like to add a small blurb to your system information card, you can add a *system description*. To do so, use the `pk;system description` command, as follows:

    pk;system description This is my system description. Hello. Lorem ipsum dolor sit amet.
    
There's a 1000 character length limit on your system description - which is quite a lot! 

If you'd like to remove your system description, just type `pk;system description` without any further parameters.

### System avatars
If you'd like your system to have an associated "system avatar", displayed on your system information card, you can add a system avatar. To do so, use the `pk;system avatar` command. You can either supply it with an direct URL to an image, or attach an image directly. For example.

    pk;s avatar http://placebeard.it/512.jpg
    pk;system avatar [with attached image]
    
To clear your avatar, simply type `pk;system avatar` with no attachment or link.

### System tags
Your system tag is a little snippet of text that'll be added to the end of all proxied messages.
For example, if you want to proxy a member named `James`, and your system tag is `| The Boys`, the final name displayed
will be `James | The Boys`. This is useful for identifying your system in-chat, and some servers may require you use
a system tag. Note that emojis *are* supported! To set one, use the `pk;system tag` command, like so:

    pk;system tag | The Boys
    pk;s tag (Test System)
    pk;s tag 🛰️

If you want to remove your system tag, just type `pk;system tag` with no extra parameters.

**NB:** When proxying, the *total webhook username* must be 32 characters or below. As such, if you have a long system name, your tag might be enough
to bump it over that limit. PluralKit will warn you if you have a member name/tag combination that will bring the combined username above the limit.
You can either make the member name or the system tag shorter to solve this. 
    
#### System server tags
If you'd like to set a system tag (as above), but only for a specific server, you can set the *system server tag*. This will override the global system tag, but only in the server you set it in. For example:

    pk;system servertag 🛰️

The server tag applies to the same server you run the command in, so this command doesn't function in DMs.

To remove an existing server-specific system tag, use the command `pk;system servertag -clear`.

::: tip
It is possible to disable the system tag for a specific server. Use the command `pk;system servertag -disable`.

To re-enable it, use the command `pk;system servertag -enable`.
:::

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
(on behalf of your system) will be in the system time zone. To do so, use the `pk;config timezone` command with a time zone name, time zone code, country code, or country emoji, like so:

    pk;config timezone Europe/Copenhagen
    pk;config timezone America/New_York
    pk;config timezone DE
    pk;config timezone 🇬🇧

You can specify time zones in various ways. In regions with large amounts of time zones (eg. the Americas, Europe, etc),
specifying an exact time zone code is the best way. To get your local time zone code, visit [this site](https://xske.github.io/tz).
You can see the full list [here, on Wikipedia](https://en.wikipedia.org/wiki/List_of_tz_database_time_zones) (see the column *TZ database name*).
You can also search by country code, either by giving the two-character [*ISO-3166-1 alpha-2* country code](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2#Officially_assigned_code_elements) (e.g. `GB` or `DE`), or just by a country flag emoji (e.g. `:flag_gb:` 🇬🇧 or `:flag_de:` 🇩🇪).

To clear a time zone, type `pk;config timezone` without any parameters. 

### Deleting a system
If you want to delete your own system, simply use the command:

    pk;system delete
    
You will need to verify by typing the system's ID when the bot prompts you to - to prevent accidental deletions.

## Member management

In order to do most things related to PluralKit, you need to work with *system members*.

Most member commands follow the format of `pk;member MemberName verb Parameter`. Note that if a member's name has multiple words, you'll need to enclose it in "double quotes" throughout the commands below (_except_ for `pk;member new`).

### Creating a member
You can't do much with PluralKit without having registered members with your system, but doing so is quite simple - just use the `pk;member new` command followed by the member's name, like so:

    pk;member new John
    pk;member new Craig Smith
    pk;member new Alice
    pk;member new Unknown
    pk;member new Skyler

`@PluralKit` will respond with a confirmation and the member ID code, like so:

    PluralKit: ✅ Member "John" (qazws) registered! 

*⚠️ As the one exception to the rule above, if the name consists of multiple words you must *not* enclose it in double quotes.*

*ℹ️ You can also use `pk;m` as shorthand for `pk;member`.*

### Looking up member info
To view information about a member, there are a couple ways to do it. Either you can address a member by their name (if they're in your own system), by their 5-character *member ID*, or by their *display name*, like so:

    pk;member John
    pk;m qazws
    pk;m J

Member IDs are the only way to address a member in another system, and you can find it in various places - for example the system's member list, or on a message info card gotten by reacting to messages with a question mark.

### Listing system members
To list all the members in a system, use the `pk;system list` command. This will show a paginated list of all member names in the system. You can either run it on your own system, or another - like so:

    pk;system list
    pk;system @Craig#5432 list
    pk;system qazws list

*ℹ️ You can also use `l` as shorthand for `list`.*

If you run `pk;system info @Craig#5432` after the example setup so far, `@PluralKit` will output something like this:

    My System Name
    | Tag | Linked accounts       | Members (5) |
    |-----|-----------------------|-------------|
    |  🛰️  | Craig#5432 (@Craig 🛰️) | (see pk;system abcde list or pk;system abcde list full) |        
    System ID: abcde | Created on 2023-03-16 00:01:52 GMT
        
If you want a more detailed list, with fields such as pronouns and description, add the word `full` to the end of the command, like so:

    pk;system list full
    pk;s @Craig#5432 l full
    pk;s qazws l full

### Member renaming
If you want to change the name of a member, you can use the `pk;member rename` command, like so:

    pk;member John rename Joanne
    pk;m "Craig Smith" rename "Craig Johnson"

### Member display names
Normally, when proxying a member, the name displayed in the proxied message will be the member's name. However, in some cases
you may want to display a different name. For example, you may want to include a member's pronouns inside the proxied name,
indicate a subsystem, include emojis or symbols that don't play nice with the command syntax, or just in general show a different name from the member's "canonical" name.

In such cases you can set the member's *display name*. Which will, well, display that name instead. You can set
a display name using the `pk;member displayname` command, like so:

    pk;member Joanne displayname Jo
    pk;m Skyler displayname Sky (he/him)

To remove a display name, just use the same command with no last parameter, eg:

    pk;member Joanne displayname

This will remove the display name, and thus the member will be proxied with their canonical name.

#### Member server display names
If you'd like to set a display name (as above), but only for a specific server, you can set the member's *server display name*.
This functions just like global display names, but only in the same server you set them in. For example:

    pk;member Joanne servername AdminJo
    
The server name applies to the same server you run the command in, so naturally this command doesn't function in DMs (as you cannot proxy in DMs).
    
### Member description
In the same way as a system can have a description, so can a member. You can set a description using the `pk;member description` command, like so:

    pk;member Joanne description Joanne is a very cool person, and you should give them hugs.
    
As with system descriptions, the member description has a 1000 character length limit. 
To clear a member description, use the command with no additional parameters (eg. `pk;member Joanne description`).

### Member color
A system member can have an associated color value.
This color is *not* displayed as a name color on proxied messages due to a Discord limitation,
but it's shown in member cards, and it can be used in third-party apps, too.
To set a member color, use the `pk;member color` command with [a hexadecimal color code](https://htmlcolorcodes.com/), like so, using either the member name or display name:

    pk;member Jo color #ff0000
    pk;m Skyler color #87ceeb

To clear a member color, use the command with no color code argument (eg. `pk;member Joanne color`).

### Member avatar
If you want your member to have an associated avatar to display on the member information card and on proxied messages, you can set the member avatar. To do so, use the `pk;member avatar` command. You can either supply it with an direct URL to an image, or attach an image directly. For example.

    pk;member Jo avatar http://placebeard.it/512.jpg
    pk;m "Craig Johnson" avatar   (with an attached image)
    
To preview the current avatar (if one is set), use the command with no arguments:

    pk;member Joanne avatar
    
To clear your avatar, use the subcommand `avatar clear` (eg. `pk;member Joanne avatar clear`).

#### Member proxy avatar
If you want your member to have a different avatar for proxies messages than the one displayed on the member card, you can set a proxy avatar. To do so, use the `pk;member proxyavatar` command, in the same way as the normal avatar command above:

    pk;member Joanne avatar
    pk;m Joanne proxyavatar http://placebeard.it/512.jpg
    pk;m "Craig Johnson" proxyavatar    (with an attached image)

#### Member server avatar
You can also set an avatar for a specific server. This will "override" the normal avatar, and will be used when proxying messages and looking up member cards in that server. To do so, use the `pk;member serveravatar` command, in the same way as the normal avatar command above:

    pk;member Joanne serveravatar
    pk;m Joanne serveravatar http://placebeard.it/512.jpg
    pk;m "Craig Johnson" serveravatar   (with an attached image)
    pk;m Joanne serveravatar clear

### Member pronouns
If you want to list a member's preferred pronouns, you can use the pronouns field on a member profile. This is a free text field, so you can put whatever you'd like in there (with a 100 character limit), like so:

    pk;member Joanne pronouns she/them
    pk;m "Craig Johnson" pronouns anything goes, really
    pk;m Skyler pronouns xe/xir, he/him, or they/them

To remove a member's pronouns, use the command with no pronoun argument (eg. `pk;member Jo pronouns`).

### Member birthdate 
If you want to list a member's birthdate on their information card, you can set their birthdate through PluralKit using the `pk;member birthdate` command. Please use [ISO-8601 format](https://xkcd.com/1179/) (`YYYY-MM-DD`) for best results, like so:

    pk;member Jo birthdate 1996-07-24
    pk;m "Craig Johnson" birthdate 2004-02-28

You can also set a birthdate without a year, either in `MM-DD` format or `Month Day` format, like so:

    pk;member Joanne birthdate 07-24
    pk;m "Craig Johnson" birthdate Feb 28
    
To clear a birthdate, use the command with no birthday argument (eg. `pk;member Joanne birthdate`).

### Deleting members
If you want to delete a member, use the `pk;member delete` command, like so:

    pk;member Joanne delete
    
You'll need to confirm the deletion by replying with the member's ID when the bot asks you to - this is to avoid accidental deletion.

## Proxying
Proxying is probably the most important part of PluralKit. This allows you to essentially speak "as" the member,
with the proper name and avatar displayed on the message. To do so, you must at least [have created a member](#creating-a-system).

### Setting up proxy tags
You'll need to register a set of *proxy tags*, which are prefixes and/or suffixes you "enclose" the real message in, as a signal to PluralKit to indicate
which member to proxy as. Common proxy tags include `[square brackets]`, `{curly braces}` or `A:letter prefixes`.

To set a proxy tag, use the `pk;member proxy` command on the member in question. You'll need to provide a "proxy example", containing the word `text`.

For example, if you want square brackets, the proxy example must be `[text]`. If you want a letter or emoji prefix, make it something like `A:text` or `🏳️‍🌈:text`. For example:

    pk;member Alice proxy ✨:text
    pk;m "Craig Johnson" proxy {text}
    pk;m Jo proxy [text]
    pk;m Skyler proxy S:text

You can now type a message enclosed in / prefixed by your proxy tags, and it'll be deleted by PluralKit and reposted with the appropriate member name and avatar (if set).

*⚠️ If you use `pk;member proxy` withoug "add", it will **replace** the proxy tag(s) for that member. `@PluralKit` will respond with a warning about this, and won't do it unless you click the `Replace` button on that message.*

*⚠️ Currently, you can't use `<angle brackets>` as proxy tags, due to a bug where custom server emojis will (wrongly) be interpreted as proxying with that member (see [issue #37](https://github.com/PluralKit/PluralKit/issues/37)).*

### Using multiple distinct proxy tag pairs
If you'd like to proxy a member in multiple ways (for example, a name or a nickname, uppercase and lowercase variants, etc), you can add multiple tag pairs.
When proxying, you may then use any of the tags to proxy for that specific member.

To add a proxy tag to a member, use the `pk;member proxy add` command:

    pk;member Alice proxy add A:text
    pk;m Joanne proxy add J:text
    pk;m Craig proxy add C:text
    pk;m Unknown proxy add ?text?
    pk;m Unknown proxy add 🤷text

To make proxy tags case-insensitive, use:

    pk;config proxy case off

### Removing tags
    
To remove a proxy tag from a member, use the `pk;member proxy remove` command:

    pk;member Joanne proxy remove [text]
    pk;m "Craig Johnson" proxy remove C:text

### Keeping your proxy tags
If you'd like your proxied messages to include the proxy tags, you can enable the "keep proxy tags" option for a given member, like so:

    pk;member Joanne keepproxy on

Turning the option off is similar - replace "on" with "off" in the command. The default value for every member is off. When proxying
a member with multiple proxy tags, the proxy tag used to trigger a given proxy will be included.

The practical effect of this is:
* **Keep proxy tags on:** `[Message goes here]` -> [Message goes here]
* **Keep proxy tags off:** `[Message goes here]` -> Message goes here 

### Disabling proxying on a per-server basis
If you need to disable or re-enable proxying messages for your system entirely in a specific server (for example, if you'd like to
use a different proxy bot there), you can use the commands:
    
    pk;system proxy off
    pk;s proxy on

## Results so far

Using the examples so far (ignoring the remove commands), if you run `pk;system list @Craig#5432`, `@PluralKit` will now output something like this:

    Members of My System Name (abcde)
    [eafas] Alice (✨:text, A:text)
    [bjeoi] Craig Johnson ({text}, C:text)
    [qazws] Joanne (tags [text], J:text)
    [wefje] Skyler (S:text)
    [nxzpa] Unknown (?text?, 🤷text)
    Sorting by name. 4 results.

and `pk;system list full @Craig#5432` will output something like this:

    Members of My System Name (abcde)

    Alice
    ID: eafas
    Proxy tags: ✨:text, A:text

    Craig Johnson
    ID: bjfeoi
    Pronouns: anything goes, really
    Birthdate: 2004-02-28
    Proxy tags: {text}, C:text

    Joanne
    ID: qazws
    Display name: AdminJo
    Pronouns: she/them
    Birthdate: 1996-07-24 
    Proxy tags: tags [text], J:text

    Skyler
    ID: wefje
    Display name: Sky (he/him)
    Pronouns xe/xir, he/him, or they/them
    Proxy tags: S:text

    Unknown
    ID: nxzpa
    Proxy tags: ?text?, 🤷text

    Sorting by name. 5 results.


## Interacting with proxied messages

### Your own messages
Since the messages will be posted by PluralKit's webhook, there's no way to edit, delete, or change the message as you would a normal user message. However, PluralKit has commands for that.

#### Editing messages
To edit a PluralKit-proxied message, reply to it with the command `pk;edit` (or as shorthand, `pk;e`) with the replacement text.

If you want to edit your last message, you can leave out the reply.

For example:

    Helo, friends!
    pk;e Hello, friends!

*⚠️ This only works if you make the edit within 10 minutes of the original message.*

#### Reproxying messages
If you are using [autoproxy](#autoproxy) and accidentally used the wrong proxy tag or forgot about your latch/switch status, reply to it with the command `pk;reproxy <member name>` (or as shorthand, `pk;rp`).

If you want to reproxy your last message, you can leave out the reply.

For example:

    a: Hi, this is Sky.
    pk;rp Skyler

will change the first message from:

    Alice: Hi, this is Sky.

to:

    Sky (he/him): Hi, this is Sky.

*⚠️ You must use the full member name, *not* their member tag.*

*⚠️ This only works on your last message, or a message sent within the last 1 minute.*

#### Deleting messages
To delete a PluralKit-proxied message, you can react to it with the `:x:` :x: emoji. Note that this only works if the message has
been sent from your own account.

### Anyone's messages

#### Querying message information
If you want information about a proxied message (eg. for moderation reasons), you can query the message for its sender account, system, member, etc.

You can
* react to the message itself with the `:grey_question:` :grey_question: or `:question:` :question: emoji, which will DM you information about the message in question,
* reply to the mssage with `pk;message` (or as shorthand, `pk;msg`), or
* use the `pk;message` command followed by [the message's ID](https://support.discord.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID-).

#### Pinging the user who sent it
If you'd like to "ping" the account behind a proxied message without having to query the message and ping them yourself,
you can react to the message with the `:bell:` :bell: or `:exclamation:` :exclamation: emoji (or even `:ping_pong:` :ping_pong:), and PluralKit will ping the relevant member and account in the same
channel on your behalf with a link to the message you reacted to.

## Autoproxy
The bot's *autoproxy* feature allows you to have messages be proxied without directly including the proxy tags. Autoproxy can be set up in various ways. There are three autoproxy modes currently implemented:

To see your system's current autoproxy settings, simply use the command:

    pk;autoproxy
    
To disable autoproxying for the current server, use the command:

    pk;autoproxy off

*ℹ️ You can also use `pk;ap` or `pk;auto` as shorthand for `pk;autoproxy`.*

::: tip
To disable autoproxy for a single message, add a backslash (`\`) to the beginning of your message.
To clear which member is currently latch, add a double backslash (`\\`) to the beginning of your message.
:::

#### Front mode
This autoproxy mode will proxy messages as the current *first* fronter of the system. If you register a switch with `Alice` and `Skyler`, messages without proxy tags will be autoproxied as `Alice`.
To enable front-mode autoproxying for a given server, use the following command:

    pk;ap front
    
#### Latch mode
This autoproxy mode will essentially "continue" previous proxy tags. If you proxy a message with `Alice`'s proxy tags, messages posted afterwards will be proxied as Alice. Proxying again with someone else's proxy tags, say, `Skyler`, will cause messages *from then on* to be proxied as Skyler.
In other words, it means proxy tags become "sticky". This will carry over across all channels in the same server.

To enable latch-mode autoproxying for a given server, use the following command:

    pk;ap latch

To set the latched member, use their proxy tags. To disable autoproxy for a single message, start a message with one backslash (`\`). To clear the current latched member, start a message with two backslashes (`\\`).

#### Member mode 
This autoproxy mode will autoproxy for a specific selected member, irrelevant of past proxies or fronters.

To enable member-mode autoproxying for a given server, use the following command, where `<member>` is a member name (in "quotes" if multiple words) or 5-letter ID:

    pk;autoproxy <member>

### Changing the latch timeout duration
By default, latch mode times out after 6 hours. It is possible to change this:

    pk;autoproxy timeout <new duration>

To reset the duration, use the following command:

    pk;ap timeout reset

To disable timeout (never timeout), use the following command:

    pk;ap timeout disable

### Disabling front/latch autoproxy on a per-member basis
If a system uses front or latch mode autoproxy, but one member prefers to send messages through the account (and not proxy), you can disable the front and latch modes for that specific member.

    pk;member <name> autoproxy off

To re-enable front / latch modes for that member, use the following command:

    pk;m <name> autoproxy on

This will *not* disable member mode autoproxy. If you do not wish to autoproxy, please turn off autoproxy instead of setting autoproxy to a specific member.

### Disabling autoproxy per-account

It is possible to fully disable autoproxy for a certain account linked to your system. For example, you might want to do this if a specific member's name is shown on the account.

To disable autoproxy for the current account, use the following command:

    pk;autoproxy account disable

To re-enable autoproxy for the current account, use the following command:

    pk;ap account enable

::: tip
This subcommand can also be run in DMs.
:::

### Example usage

For example, using [the setup example above](#setting-up-proxy-tags), `@Craig#5432` can type this:

    pk;ap latch
    I haven't used a tag yet, so this message comes from @Craig#5432
    ✨: hello, this is Alice via autoproxy (using an explicit prefix tag)
    this is still Alice (using latch without tag)
    \I'm sending this message one-off as @Craig#5432, without proxy
    but I'm still latched! (this also is sent from Alice via autoproxy)
    [hello, this is Joanne, using autoproxy and a surround tag]
    still Jo on latch!
    j: I could use my prefix or surround tags if I want, but don't have to
    because the last time I used a tag, I used mine (Jo's)
    \\now I'm clearing latch; this is from @Craig#5432
    and now new messages will be from @Craig#5432 because latch is cleared
    🤷 but autoproxy is still on
    so this is Unknown

and the result will look like this:

    @Craig#5432: pk;ap latch
    @Craig#5432: I haven't used a tag yet, so this message comes from @Craig#5432
    Alice: hello, this is Alice via autoproxy (using an explicit prefix tag)
    Alice: this is still Alice (using latch without tag)
    @Craig#5432: I'm sending this message one-off as @Craig#5432, without proxy
    Alice: but I'm still latched! (this also is sent from Alice via autoproxy)
    AdminJo: hello, this is Joanne, using autoproxy and a surround tag
    AdminJo: still Jo on latch!
    AdminJo: I could use my prefix or surround tags if I want, but don't have to
    AdminJo: because the last time I used a tag, I used mine (Jo's)
    @Craig#5432: now I'm clearing latch; this is from @Craig#5432
    @Craig#5432: and now new messages will be from @Craig#5432 because latch is cleared
    Unknown: but autoproxy is still on
    Unknown: so this is Unknown

## Managing switches
PluralKit allows you to log member switches through the bot.
Essentially, this means you can mark one or more members as *the current fronter(s)* for the duration until the next switch.
You can then view the list of switches and fronters over time, and get statistics over which members have fronted for how long.

### Logging switches
To log a switch, use the `pk;switch` command with one or more members. For example:

    pk;switch Joanne
    pk;switch "Craig Johnson" Joanne
    
Note that the order of members are preserved (this is useful for indicating who's "more" at front, if applicable).
If you want to specify a member with multiple words in their name, remember to encase the name in "double quotes".

*ℹ️ You can also use `pk;sw` as shorthand for `pk;switch`.*

### Switching out
If you want to log a switch with *no* members, you can log a switch-out as follows:

    pk;sw out

### Moving switches
If you want to log a switch that happened further back in time, you can log a switch and then *move* it back in time, using the `pk;switch move` command.
You can either specify a time either in relative terms (X days/hours/minutes/seconds ago) or in absolute terms (this date, at this time).
Absolute times will be interpreted in the [system time zone](#setting-a-system-time-zone). For example:

    pk;switch move 1h
    pk;sw move 4d12h
    pk;sw move 2 PM
    pk;sw move May 8, 4:30 PM

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
    pk;s @Craig#5432 fronter
    pk;s qazws fronter

### Querying front history
To look at the front history of a system (currently limited to the last 10 switches). use the `pk;system fronthistory` command, for example:

    pk;system fronthistory
    pk;s @Craig#5432 fronthistory
    pk;s qazws fronthistory
    
### Querying front percentage
To look at the per-member breakdown of the front over a given time period, use the `pk;system frontpercent` command. If you don't provide a time period, it'll default to 30 days. For example:

    pk;system frontpercent
    pk;s @Craig#5432 frontpercent 7d
    pk;s qazws frontpercent 100d12h

Note that in cases of switches with multiple members, each involved member will have the full length of the switch counted towards it. This means that the percentages may add up to over 100%.
<br> It is possible to disable this with the `-flat` flag; percentages will then add up to 100%.

::: tip
If you use the `switch-out` function, the time when no-one was fronting will show up in front history as "no fronter". To disable this, use the `-fronters-only`, or `-fo` flag:

```
pk;system frontpercent -fronters-only
```
:::

## Member groups
PluralKit allows you to categorize system members in different **groups**.
You can add members to a group, and each member can be in multiple groups.
The groups a member is in will show on the group card.

### Creating a new group
To create a new group, use the `pk;group new` command:

    pk;group new MyGroup
    
This will create a new group. Groups all have a 5-letter ID, similar to systems and members.

*ℹ️ You can also use `pk;g` as shorthand for `pk;group`.*

### Adding and removing members to groups
To add a member to a group, use the `pk;group <group> add` command, eg:

    pk;group MyGroup add "Craig Johnson"
    
You can add multiple members to a group by separating them with spaces, eg:

    pk;g MyGroup add Joanne Skyler Alice

Similarly, you can remove members from a group, eg:

    pk;g MyGroup remove Skyler
    
### Listing members in a group
To list all the members in a group, use the `pk;group <group> list` command.
The syntax works the same as `pk;system list`, and also allows searching and sorting, eg:

    pk;group MyGroup list
    pk;g MyGroup list --by-message-count jo
    
### Listing all your groups
In the same vein, you can list all the groups in your system with the `pk;group list` command:

    pk;group list
    
### Group name, description, icon, delete
(TODO: write this better)

Groups can be renamed:

    pk;group MyGroup rename SuperCoolGroup

Groups can have icons that show in on the group card:
    
    pk;g MyGroup icon https://my.link.to/image.png
    
Groups can have descriptions:

    pk;g MyGroup description This is my cool group description! :)
    
Groups can be deleted:

    pk;g MyGroup delete

## Privacy
There are various reasons you may not want information about your system or your members to be public. As such, there are a few controls to manage which information is publicly accessible or not.

### System privacy
At the moment, there are four aspects of system privacy that can be configured.

- System description
- Current fronter
- Front history
- Member list

Each of these can be set to **public** or **private**. When set to **public**, anyone who queries your system (by account or system ID, or through the API), will see this information. When set to **private**, the information will only be shown when *you yourself* query the information. The cards will still be displayed in the channel the commands are run in, so it's still your responsibility not to pull up information in servers where you don't want it displayed.

To update your system privacy settings, use the following commands:

    pk;system privacy <subject> <level>
    
where `<subject>` is either `description`, `fronter`, `fronthistory` or `list`, corresponding to the options above, and `<level>` is either `public` or `private`. `<subject>` can also be `all` in order to change all subjects at once.

For example:

    pk;system privacy description private
    pk;s privacy fronthistory public
    pk;s privacy list private

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

As with system privacy, each can be set to **public** or **private**. The same rules apply for how they are shown, too. When set to **public**, anyone who queries your system (by account or system ID, or through the API), will see this information. When set to **private**, the information will only be shown when *you yourself* query the information. The cards will still be displayed in the channel the commands are run in, so it's still your responsibility not to pull up information in servers where you don't want it displayed.

However, there are two catches:
- When the **name** is set to private, it will be replaced by the member's **display name**, but only if they have one! If the member has no display name, **name privacy will not do anything**. PluralKit still needs some way to refer to a member by name :) 
- When **visibility** is set to private, the member will not show up in member lists unless `-all` is used in the command (and you are part of the system).

To update a member's privacy you can use the command:

    pk;member <member> privacy <subject> <level>

where `<member>` is the name or the id of a member in your system, `<subject>` is either `name`, `description`, `avatar`, `birthday`, `pronouns`, `metadata`, or `visiblity` corresponding to the options above, and `<level>` is either `public` or `private`. `<subject>` can also be `all` in order to change all subjects at once.  
`metadata` will affect the message count, the date created, the last fronted, and the last message information.

For example:

    pk;member Joanne privacy visibility private
    pk;m "Craig Johnson" privacy description public
    pk;m Alice privacy birthday public
    pk;m Skyler privacy all private

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

Note that while Tupperbox supports features such as per-member system tags, PluralKit does not. PluralKit also does not currently support importing or exporting member groups.
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
