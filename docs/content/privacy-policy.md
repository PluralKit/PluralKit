---
title: Privacy Policy
description: I'm not a lawyer. I don't want to write a 50 page document no one wants to (or can) read. It's short, I promise.
permalink: /privacy
---

# Privacy Policy

I'm not a lawyer. I don't want to write a 50 page document no one wants to (or can) read. In short:

This is the data PluralKit collects indefinitely:
* Information *you give the bot* (eg. system/member profiles, switch history, linked accounts, etc)
* Metadata about proxied messages (sender account ID, sender system/member, timestamp)
* Aggregate anonymous usage metrics (eg. gateway events received/second, messages proxied/second, commands executed/second)
  * This is visible on [https://stats.pluralkit.me/](https://stats.pluralkit.me/)
* Nightly database backups of the above information
* High-level logs of actions taken on the bot (eg. systems created or deleted, switches logged, etc)

This is the data PluralKit does *not* collect:
* Anything not listed above, including...
* Proxied message *contents* (they are fetched on-demand from the original message object when queried)
* Metadata about deleted messages, members, switches or systems
* Information added *and deleted* between nightly backups
* Information about messages that *aren't* proxied through PluralKit

System and member information (names, member lists, descriptions, etc) are public by default, and can be looked up by anyone given a system/member ID or an account ID. This can be changed using the [privacy settings](/guide#privacy). 

You can export your system information using the `pk;export` command. This does not include message metadata (as the file would be huge). If there's demand for a command to export that, [let me know on GitHub](https://github.com/xSke/PluralKit/issues).

You can delete your information using `pk;system delete`. This will delete all system information and associated members, switches, and messages. This will not delete your information from the database backups. Contact me if you want that wiped, too.

The bot is [open-source](https://github.com/xSke/PluralKit). While I can't *prove* this is the code that's running on the production server... it is, promise.