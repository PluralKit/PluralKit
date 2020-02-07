---
layout: default
title: Tips and Tricks
permalink: /tips
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

## Permission checker command
If you're having issues with PluralKit not proxying, it may be an issue with your server's channel permission setup.
PluralKit needs the *Read Messages*, *Manage Messages* and *Manage Webhooks* permission to function.

To quickly check if PluralKit is missing channel permissions, you can use the `pk;permcheck` command in the server
in question. It'll return a list of channels on the server with missing permissions. This may include channels
you don't want PluralKit to have access to for one reason or another (eg. admin channels).

If you want to check permissions in DMs, you'll need to add a server ID, and run the command with that.
For example: `pk;permcheck 466707357099884544`. You can find this ID
[by enabling Developer Mode and right-clicking (or long-pressing) on the server icon](https://discordia.me/developer-mode).