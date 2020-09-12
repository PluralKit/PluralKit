---
title: Getting Started
description: A basic tutorial of how to set up the bot.
permalink: /start

# Previous page on sidebar is the invite link, prevent that from showing up here
prev: false
---

# Quick start

This page will get you started :zap: lightning-fast :zap: with the bot. You'll need to follow these steps:

## Create a system
First, **create a system** with the following command:

    pk;system new

::: tip
You can also specify a system name if you want:

    pk;system new My System Name

:::

## Create a member       
Second, **create a member** with the following command, inserting the member name:

    pk;member new MyMember

::: tip
You can include spaces, punctuation, or symbols in the member name. However, you'll need to write them `"in quotes"` every time you refer to the member elsewhere.

Instead, you can give your member a simple, easy to type name, then set the member's [display name](./user-guide.md#member-display-names) to a more complex version. That'll get displayed when proxying, and then you can keep the following commands simple.
::: 

## Set some proxy tags
Now, you'll need to tell PluralKit how you want to trigger the proxy using **proxy tags**. Often, these will be a pair of brackets, an emoji prefix, or something similar.

To set a member's proxy tags, you'll need to "pretend" you're proxying the word `text` - just the word itself, all-lowercase. This often gets a bit confusing, so here are a couple of examples with various patterns:

    pk;member MyMember proxy J:text
    pk;member MyMember proxy [text]
    pk;member MyMember proxy 🌸text
    pk;member MyMember proxy text -Q

::: tip
You're not limited to the types of proxy tags shown above. You can put anything you'd like around the word `text` (before, after, or both), and PluralKit will look for that. Be creative!
:::

## Set an avatar (optional)
If you want an avatar displayed, use the following command:

    pk;member MyMember avatar https://link.to.your/avatar.png

::: tip
If you don't have a link, you can leave that out entirely, and then **attach** the image to the command message itself. PluralKit will pick up on the attachment, and use that instead.
:::

::: warning
Avatars have some restrictions: 
- The image must be in **.jpg**, **.png**, or **.webp** format
- The image must be under **1024 KB** in size
- The image must be below **1024 x 1024 pixels** in resolution (along the smallest axis).
- Animated GIFs are **not** supported (even if you have Nitro).
:::

## What's next?

You could...
- [set up your member profile with descriptions, pronouns, etc](./user-guide.md#member-management)
- [log your switches](./user-guide.md#managing-switches)
- [configure privacy settings](./user-guide.md#privacy)
- or something else!

See the [User Guide](./user-guide.md) for a more complete reference of the bot's features.