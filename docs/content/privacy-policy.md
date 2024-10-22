---
title: Privacy Policy
description: This page outlines PluralKit’s privacy policy - how PluralKit collects and uses your data - in plain language.
permalink: /privacy
---

# Privacy Policy

This page outlines PluralKit’s privacy policy - **how PluralKit collects and uses your data** - in plain language.
I'm not a lawyer, and having a 50-page document here filled with legal jargon means no one will read it - so we're keeping things as simple as we can.

This version of the policy is effective from November 11th, 2024. Previous versions of this policy [can be viewed on GitHub](<https://github.com/PluralKit/PluralKit/commits/main/docs/content/privacy-policy.md>).

If you have any questions or concerns regarding this policy, please join [the support server](<https://discord.gg/PczBt78>) (preferred), or email [legal@pluralkit.me](mailto:legal@pluralkit.me).

## Data provided by you

Any information **explicitly provided by you** (eg. system/member profiles, switch history, linked accounts, etc) is collected indefinitely, and deleted immediately when you choose to remove it.

For technical reasons, there may be a short delay before images (avatars and banners) uploaded to the PluralKit CDN are fully removed.

System and member information (names, member lists, descriptions, etc) is **public by default**, and can be looked up by anyone given a system/member ID or an account ID. This can be changed using [the privacy settings](/guide/#privacy).

You can export your system information using the `pk;export` command. This does not include message metadata (as the file would be huge). If you wish to request a copy of the message metadata PluralKit has stored for your Discord account, ask a developer in the support server.

You can delete your information using `pk;system delete`. This will delete all system information, including any associated members, groups, and other information. This will not delete message metadata, which is required for moderation purposes - see below for details.

## Data required for moderation

PluralKit stores **metadata for all proxied messages**, to allow for moderation. This includes:

- Message link
- ID of original message
- ID of Discord account from which the message was sent
- ID of PluralKit member associated with the message

Metadata, by definition, does not include the content of messages proxied with the bot. PluralKit discards the content of proxied messages (and any attachments to proxied messages) after the message has been proxied.

This data is stored indefinitely, and deleted when the proxied message is deleted.

## Other

PluralKit stores other miscellaneous data, to aid in fixing any issues that may occur, and to help us know how many people are using the service. This includes:

- Aggregate **anonymous** usage metrics (eg. gateway events received/second, messages proxied/second, commands executed/second)
- High-level logs of actions taken on the bot (eg. systems created or deleted, switches logged, etc)
- High-level logs of requests to the API

PluralKit **does not collect** any data other than the above.
Any data collected by PluralKit is accessed only when **explicitly requested** by a user, or by the staff team for **abuse handling and moderation purposes**.
No data is shared with third parties by the staff team except when **required by law**.
