---
sidebarDepth: 2
---

# Frequently asked questions

## How does PluralKit proxying work? <!-- how-it-works -->
Proxying works using [Discord webhooks](https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks). Every time PluralKit sees a message, it'll:

1. Look for a pair of [proxy tags](./guide/proxying.md#proxy-tags) in the message. If it finds any, it'll determine which member they correspond to. If not, it'll check your [autoproxy] settings and proceed with those.
2. Create a webhook in the relevant channel (if one doesn't already exist).
3. Send a message through that webhook with the correct username, avatar, and message content.
4. Delete the original message.

The result is that the proxied message is sent using the webhook. This has a few implications:

### Why can't I change my name color? <!-- color -->
Webhooks aren't real Discord users, and can't have roles attached, or otherwise have their name color changed.

### Why does the name/avatar show up wrong when I click on a profile? <!-- profile-error -->
Since all webhook messages have the same ID, Discord shows a random name/avatar pair - it'll be whatever it happens to have cached.

### How can I determine who sent a message? <!-- lookup -->
If you react with the :question: emoji, PluralKit will DM you information about the message and its sender.

### How can I ping a webhook? <!-- ping -->
If you react with the :exclamation: emoji, PluralKit will send a "ping" message in the channel, pinging the original sender account on your behalf.

### How can I delete my own message? <!-- deleting -->
If you react with the :x: emoji, you can delete a proxied message if it was originally sent from your own account.

### Why can't I block webhook messages? <!-- blocking -->
Discord does not support blocking webhooks. Unfortunately, there's no workaround for this at the moment :slightly_frowning_face:

## Who can use this bot? <!-- gatekeeping -->
*or: "Can I use this bot for kin/roleplay/other non-plurality uses? Can I use it if I’m not plural myself? Is that appropriating?"*

Although this bot is designed with plural systems and their use cases in mind, the bot’s feature set is still useful for many other types of communities, including role-playing and otherkin. By all means go ahead and use it for those communities, too. **We don’t gatekeep, and neither should you.**

## Who's the mascot? <!-- mascot -->
[Our lovely bot mascot](https://imgur.com/a/LTqQHHL)'s name is **Myriad**! They were drawn by [Layl](https://twitter.com/braindemons). *(and yes, there are fictives.)*

### Why does the bot avatar occasionally change? <!-- bot-avatar -->
Every month, we accept requests for fanart of Myriad and pick out one to change the bot's avatar to. This gets us lots of variety :)

We accept submissions in the [#myriad-mythology](https://discordapp.com/channels/466707357099884544/569526847667175444) channel in the [support server](./support-server.md).

#### Bot avatar history

| Month | Artist | Avatar |
| ----: | ------ | ------ |
| *(n/a)* | [@Layl#8888](https://twitter.com/braindemons) | ![Original Myriad avatar](./files/avatars/original.png =64x64) |
| **June 2020** | @detective zikachu#8899 | ![June 2020 avatar](./files/avatars/2020-06.png =64x64) |