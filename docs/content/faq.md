---
title: FAQ
description: Frequently asked questions (and the answers to them).
permalink: /faq
---

# Frequently asked questions

## General

### What is this bot for?
PluralKit detects messages with certain prefixes and/or suffixes associated with a registered profile, then replaces that message under a "pseudo-account" of that profile using things called webhooks. This is useful for multiple people sharing one body (aka "systems"), people who wish to roleplay as different characters without having several accounts, or anyone else who may want to post messages as a different person from the same account.

### Can I use this bot for kin/roleplay/other non-plurality uses? Can I use it if I'm not plural myself? Is that appropriating?
Although this bot is designed with plural systems and their use cases in mind, the bot's feature set is still useful for many other types of communities, including role-playing and otherkin. By all means go ahead and use it for those communities, too. We don't gatekeep, and neither should you.

### Who's the mascot?
[Our lovely bot mascot](/favicon.png)'s name is Myriad! They were drawn by the lovely [Layl](https://twitter.com/braindemons). Yes, there are fictives.

### How do I suggest features to be added to PluralKit?

You can suggest features in the [support server](https://discord.gg/PczBt78)'s `#suggestions-feedback` channel. Check the `#frequent-suggestions` channel to see if your idea has already been suggested!

We also track feature requests through [Github Issues](https://github.com/PluralKit/PluralKit/issues). Feel free to open issue reports or feature requests there as well.

### How can I support the bot's development?
I (the bot author, [Ske](https://twitter.com/floofstrid)) have a Patreon. The income from there goes towards server hosting, domains, infrastructure, my Monster Energy addiction, et cetera. There are no benefits. There might never be any. But nevertheless, it can be found here: [https://www.patreon.com/floofstrid](https://www.patreon.com/floofstrid)

## Privacy / safety

### Who has access to the bot's data?

The only people with access to the database or the information the bot processes is the developer, no one else has access. More information about how information is processed is described on the [Privacy Policy](/privacy).

But in short: the bot does not save or log messages beyond metadata necessary for the bot's functioning, and we do not have the ability to read messages going through the bot, proxied or not. The bot is [open-source](https://github.com/PluralKit/PluralKit), so anyone with technical knowledge can confirm this.

### I set all my privacy options to private. Why can others still see some information?
There are two possible answers here:

1. Your system privacy options are set to private. You must set *each member*'s privacy options to private if you wish to hide that member's information.

2. There is some information that, for moderation reasons, must always remain public.
This includes:
  * a system's linked accounts;
  * the information that would be shown when a member proxies (proxy name/avatar image);
  * and the system ID on a member card.

For more information about why certain information must be public, see [this github issue](https://github.com/PluralKit/PluralKit/issues/238).

### Is there a way to restrict PluralKit usage to a certain role? / Can I remove PluralKit access for specific users in my server?
This is not a feature currently available in PluralKit. It may be added in the future.
In the meantime, this feature is supported in Tupperbox (an alternative proxying bot) - ask about it in their support server: <https://discord.gg/Z4BHccHhy3>

### Is it possible to block proxied messages (like blocking a user)?
No. Since proxied messages are posted through webhooks, and those technically aren't real users on Discord's end, it's not possible to block them. Blocking PluralKit itself will also not block the webhook messages. Discord also does not allow you to control who can receive a specific message, so it's not possible to integrate a blocking system in the bot, either. Sorry :/

### What can people see if I have all the privacy options on a member set to private?
You can see an example of exactly this by querying `cmpuv` with the command `pk;m cmpuv`. (Also useful: there is an example system with every field filled, you can use `pk;s exmpl`)

## Known issues

### The name color doesn't work/can we color our proxy names?
No. This is a limitation in Discord itself, and cannot be changed. The color command instead colors your member card that come up when you type `pk;member <member name>`.

### Why does my avatar not work?
* PluralKit doesn't check if the avatar will be accepted by Discord. If you just set your avatar and it's not showing up, please try a different avatar.
* The message with the avatar may have been deleted, or the link may have become invalid. Try re-setting your avatar.
* Discord sometimes has issues displaying avatars. We can't do anything about that, sorry :(

### Why can't I use nitro emoji in some channels?
* Webhooks inherit nitro emoji permissions from the `@everyone` role, so `@everyone` must have the "Use External Emoji" permission to be able to use nitro emoji with PluralKit.
If it still doesn't work, make sure this permission isn't denied in channel overrides (found in channel settings -> permissions). You can also check if it's a permissions issue with `pk;permcheck`.
* PluralKit must be in the server the emojis are from. This is because of a change made by Discord in 2022.
* Because PluralKit cannot be a Twitch subscriber, it will never be able to use emojis from Twitch integrations.

### Why can't I invite PluralKit to my server?

You might not be logged into the invite screen. Make sure you're logged into Discord in your browser, and check that you have permissions to add the bot to the server. If it still doesn't work, try logging out of Discord in your browser, logging back in, then re-inviting the bot.

If you are on mobile and are having issues, try copy-pasting the link into your browser instead of tapping it.

### Why are my member lists broken on mobile?
This is a bug in Discord's mobile client. It handles formatting slightly different than other clients. This can't be fixed without breaking things elsewhere, so we're waiting for a fix on their end.

### Why is my time showing up incorrectly after Daylight savings time change?
You probably set your timezone in PluralKit to a specific timezone, and PluralKit doesn't know how to calculate daylight savings time with that. Instead, set your timezone to a city (for example, "America/Toronto") - you can find your correct city identifier on <https://xske.github.io/tz>

### Why am I not able to edit a message via ID? or, Why is PluralKit editing the wrong message?
It is not possible to edit messages via ID. Please use the full link, or reply to the message.

