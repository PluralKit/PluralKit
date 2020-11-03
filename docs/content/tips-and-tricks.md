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
|pk;system|pk;s|
|pk;system list|pk;s l|
|pk;system list full|pk;s l f|
|pk;system fronter|pk;s f|
|pk;system fronthistory|pk;s fh|
|pk;system frontpercent|pk;s fp|
|pk;member|pk;m|
|pk;member new|pk;m n|
|pk;switch|pk;sw|
|pk;message|pk;msg|
|pk;autoproxy|pk;ap|

## Member list flags
There are a number of option flags that can be added to the `pk;system list` command.

### Sorting options
|Flag|Aliases|Description|
|---|---|---|
|-by-name|-bn|Sort by member name (default)|
|-by-display-name|-bdn|Sort by display name|
|-by-id|-bid|Sort by member ID|
|-by-message-count|-bmc|Sort by message count (members with the most messages will appear near the top)|
|-by-created|-bc|Sort by creation date (members least recently created will appear near the top)|
|-by-last-fronted|-by-last-front, -by-last-switch, -blf, -bls|Sort by most recently fronted|
|-by-last-message|-blm, -blp|Sort by last message time (members who most recently sent a proxied message will appear near the top)|
|-by-birthday|-by-birthdate, -bbd|Sort by birthday (members whose birthday is in January will appear near the top)|
|-reverse|-rev, -r|Reverse previously chosen sorting order|
|-random||Sort randomly|

### Filter options
|Flag|Aliases|Description|
|---|---|---|
|-all|-a|Show all members, including private members|
|-public-only|-public, -pub|Only show public members (default)|
|-private-only|-private, -priv|Only show private members|

::: warning
You cannot look up private members of another system.
:::

### Additional fields to include in the search results
|Flag|Aliases|Description|
|---|---|---|
|-with-last-switch|-with-last-fronted, -with-last-front, -wls, -wlf|Show each member's last switch date|
|-with-last-message|-with-last-proxy, -wlm, -wlp|Show each member's last message date|
|-with-message-count|-wmc|Show each member's message count|
|-with-created|-wc|Show each member's creation date|