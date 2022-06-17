---
layout: default
permalink: /tips
title: Tips and Tricks

# Next page on sidebar is the support server link, prevent that from showing up here
next: false
---

# Tips and Tricks

## Use command shorthands
PluralKit has a couple of useful command shorthands to reduce the typing:

|Original|Shorthand|
|---|---|
|sp;system|sp;s|
|sp;system list|sp;s l|
|sp;system list full|sp;s l f|
|sp;system fronter|sp;s f|
|sp;system fronthistory|sp;s fh|
|sp;system frontpercent|sp;s fp|
|sp;member|sp;m|
|sp;member new|sp;m n|
|sp;group|sp;g|
|sp;group new|sp;g n|
|sp;switch|sp;sw|
|sp;message|sp;msg|
|sp;autoproxy|sp;ap|

## Member list flags
There are a number of option flags that can be added to the `sp;system list` command.

### Sorting options
|Flag|Aliases|Lists|Description|
|---|---|---|---|
|-by-name|-bn|Member, Group|Sort by name (default)|
|-by-display-name|-bdn|Member, Group|Sort by display name|
|-by-id|-bid|Member, Group|Sort by ID|
|-by-message-count|-bmc|Member|Sort by message count (members with the most messages will appear near the top)|
|-by-created|-bc|Member, Group|Sort by creation date (least recently created will appear near the top)|
|-by-last-fronted|-by-last-front, -by-last-switch, -blf, -bls|Member|Sort by most recently fronted|
|-by-last-message|-blm, -blp|Member|Sort by last message time (members who most recently sent a proxied message will appear near the top)|
|-by-birthday|-by-birthdate, -bbd|Member|Sort by birthday (members whose birthday is in January will appear near the top)|
|-reverse|-rev, -r|Member, Group|Reverse previously chosen sorting order|
|-random||Member, Group|Sort randomly|

### Filter options
|Flag|Aliases|Lists|Description|
|---|---|---|---|
|-all|-a|Member, Group|Show all members/groups, including private members/groups|
|-private-only|-po|Member, Group|Only show private members/groups|

::: warning
You cannot look up private members or groups of another system.
:::

### Additional fields to include in the search results
|Flag|Aliases|Lists|Description|
|---|---|---|---|
|-with-last-switch|-with-last-fronted, -with-last-front, -wls, -wlf|Member|Show each member's last switch date|
|-with-last-message|-with-last-proxy, -wlm, -wlp|Member|Show each member's last message date|
|-with-message-count|-wmc|Member|Show each member's message count|
|-with-created|-wc|Member, Group|Show each item's creation date|
|-with-avatar|-wa, -wi, -ia, -ii, -img|Member, Group|Show each item's avatar URL|
|-with-pronouns|-wp -wprns|Member|Show each member's pronouns in the short list (shown by default in full list)|
|-with-displayname|-wdn|Member, Group|Show each item's displayname|

## Miscellaneous flags
|Command|Flag|Aliases|Description|
|---|---|---|---|
|List commands|-search-description|-sd|Search inside descriptions instead of member/group names|
|sp;system frontpercent|-fronters-only|-fo|Show the system's frontpercent without the "no fronter" entry|
|sp;system frontpercent|-flat||Show "flat" frontpercent - percentages add up to 100%|
|sp;group \<group> frontpercent|-fronters-only|-fo|Show a group's frontpercent without the "no fronter" entry|
|sp;group \<group> frontpercent|-flat||Show "flat" frontpercent - percentages add up to 100%|
|sp;edit|-append||Append the new content to the old message instead of overwriting it|
|sp;edit|-prepend||Prepend the new content to the old message instead of overwriting it|
|Most commands|-all|-a|Show hidden/private information|
|Most commands|-raw|-r|Show text with formatting, for easier copy-pasting|
|All commands|-private|-priv|Show private information|
|All commands|-public|-pub|Hide private information|
|All commands, except `delete`|-y|-yes|Skip confirmation prompt|
