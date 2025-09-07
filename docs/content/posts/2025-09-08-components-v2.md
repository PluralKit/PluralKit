---
title: on the switch to Components V2
permalink: /posts/2025-09-08-components-v2/
---

## on the switch to Components V2

you probably will have noticed the new design of system/member/group cards
in PluralKit. we know that a lot of people will have questions as to why
we've suddenly changed the layout & design of the cards, so we hope this
post can explain some of the decisions that went into these.

### why change the cards at all?

the old cards used something Discord calls "embeds." embeds were initially
only meant for showing details about a link posted in chat, but were later
also used by Discord bots for displaying information in a structured way.

embeds are extremely limited in their layout, and bots have very little
control over how things are displayed in them. in addition, a lot of newer
Discord features (things like the Markdown headers, or small text) either
don't display in embeds at all, or only display on some platforms - making
them very inconsistent in how they display.

the new cards use Discord's "Components V2" - which has been designed by
Discord from the ground up specifically for bots to use for custom content.
Components V2 is much more flexible, and fixes a lot of the issues with the
old embeds (the Markdown headers / small text being one example), letting us
have a lot more control over the display of the cards.

Components V2 is the way forward, as far as Discord is concerned - meaning
that some of the issues with embeds will never be fixed in embeds themselves,
necessitating a move to Components V2.

### continual improvement (also: "where'd my proxy avatar go?")

currently, the only thing missing from PluralKit's new Components V2 cards
(compared to the previous embed-based cards) is member proxy avatars. on
the old member cards, proxy avatars would display as a tiny circle at the
top left. there is not currently a way to display an image of that size/
position in Components V2... but from what we understand, that is on the
list of things Discord will be adding in the future. which leads into the
next point:

unlike embeds, which have remained stagnant for years, Discord are actively
working on adding *new functionality* to Components V2, as well as fixing
whatever issues arise - in order to take advantage of new functionality as
Discord release it, we would have needed to move to Components V2 at some
point. we figured that with the state Components V2 is currently in, now
was a good time to make that switch!

### character limits

another advantage of Components V2 over embeds is the character limit for
cards. the old embeds had a hard limit of 1024 characters in a single field,
with a limit of 2000 characters for the entire embed. this is the reason that
PK descriptions are capped at 1000 characters.

Components V2, however, has a character limit of *6000* characters across
the entire card, which can be split however we like. this means that once the
old embed-based cards are removed, we will be able to raise the description
character limit!

### other small improvements

- Components V2 allows us to use real code blocks in the card footers for
  things like system/member/group IDs. on mobile Discord clients, this makes
  copying IDs a lot easier - you can copy an individual code block's content
  by tapping on it
- having a banner image set no longer makes the description width smaller
- on mobile clients, it is now a lot easier to view any images larger, just
  by tapping on them
- some PluralKit users who use screen readers have reported that the new
  Components V2 cards are read by their screen readers in a much more easily
  understood way

### the new cards don't show up! / is there a way to see the old cards?

Components V2 is not supported on older Discord clients. there is nothing
we can do about this, other than encourage you to update your Discord
client.

however - for now, using the `-show-embed` (or `-se`) flag to the
`pk;system`, `pk;member`, and `pk;group` commands will show the old
embed-based cards.

the old cards will still show in some places in the bot (the most prominent
example being when querying message info with the ❓ reaction) also,
until we migrate those parts of the bot to use Components V2. 

the old embed-based cards will be removed from the bot in future - although
we do not have any specific timeframe in mind for this yet.

### in closing

we hope that this gives you a bit more context as to why we've made this
change - although there are some new design choices here, this was not
a change made just for the sake of changing.

a lot of the decisions that went into the new versions of the cards were
iterated on with feedback from members of the community who help beta test
new PluralKit features - i want to thank those people immensely for their
input!

if you have any questions, please let us know in [the support server](https://discord.gg/PczBt78).
