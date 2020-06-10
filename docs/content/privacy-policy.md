# Privacy policy

::: tip
The bot is open-source (under the [Apache License, version 2.0](https://www.apache.org/licenses/LICENSE-2.0)), and the source can be seen [on GitHub](https://github.com/xSke/PluralKit).
::: 

I’m not a lawyer. I don’t want to write a 50 page document no one wants to (or can) read. In short:

## What we store (and don't store)
This is the data PluralKit stores indefinitely:
* The information *you give the bot* (eg. system/member profiles, switch history, linked accounts)
* Metadata about proxied messages (sender account ID, message IDs, sender system and member)
* Anonymous aggregate usage metrics (eg. amount of gateway events, message proxies, executed commands per second)
    * All of this is visible on [the public stats page](https://stats.pluralkit.me/)
* Nightly database backups of the above information
* High-level logs of actions taken on the bot (systems created, switches logged, etc)
    * In practice, this is stored for 30 days, but this may change in the future.
    
This is the data PluralKit does **not** collect:
* Anything not listed above, including...
* Proxied message *contents*
* Metadata about deleted messages, members, switches, or systems
* Information added *and deleted* between nightly backups
* Any information about messages that *aren't* proxied through PluralKit.

## Managing your information
System and member information (names, member lists, descriptions, etc) is **public by default**, and can be looked up by anyone given a system/member ID or an account ID.
This can be changed using [the available privacy settings](./guide/privacy.md).

### Exporting information
You can **export your system information** using the `pk;export` command. This does not include message metadata, as the file would be huge. If there's demand for a command to export that, [let me know](https://github.com/xSke/PlualKit/issues).

### Deleting information
You can **delete your information** by deleting your system (with the `pk;system delete`) command. This will delete:
- Information about your system
- Information about all system members
- Information about all switchees
- Information about all messages

This will *not* delete information from the database backups; [contact me](./support-server.md) if you want those wiped (or for that matter, need a data restore).