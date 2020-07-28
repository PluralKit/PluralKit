# Moderation tools
Since PluralKit proxies work by deleting and reposting messages through webhooks, some of Discord's standard moderation tools won't function.

Specifically, you can't kick or ban individual members of a system; all moderation actions have to be taken on the concrete Discord account.

## Identifying users
You can use PluralKit's lookup tools to connect a message to the sender account. This allows you to use standard moderation tools on that account (kicking, banning, using other moderation tools, etc).

### Querying messages
To look up which account's behind a given message (as well as other information), you can either:

- React to the message with the :question: emoji, which will DM you a message card
- Use the `pk;msg <message-link>` command with the message's link, which will reply with a message card *(this also works in PluralKit's DMs)*

An example of a message card is seen below:

![Example of a message query card](../assets/ExampleQuery.png)

### Looking up systems and accounts
Looking up a system by its 5-character ID (`exmpl` in the above screenshot) will show you a list of its linked account IDs. For example:

    pk;system exmpl

You can also do the reverse operation by passing a Discord account ID (or a @mention), like so:

    pk;system 466378653216014359

Both commands output a system card, which includes a linked account list. These commands also work in PluralKit's DMs.

### System tags
A common rule on servers with PluralKit is to enforce system tags. System tags are a little snippet of text, a symbol, an emoji, etc, that's added to the webhook name of every message proxied by a system. A system tag will allow you to identify members that share a system at a glance. Note that this isn't enforced by the bot; this is simply a suggestion for a helpful server policy :slightly_smiling_face:

## Blocking users
It's not possible to block specific PluralKit users. Discord webhooks don't count as 'real accounts', so there's no way to block them. PluralKit also can't control who gets to see a message, so there's also no way to implement user blocking on the bot's end. Sorry. :slightly_frowning_face: