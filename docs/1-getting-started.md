---
layout: default
title: Getting Started
permalink: /start
description: A basic tutorial of how to set up the bot.
nav_order: 1
---

# Getting Started
{: .no_toc }

## Table of Contents
{: .no_toc .text-delta }

1. TOC
{:toc}

## The system
The first thing you need to do to use PluralKit is to set up a system! Each account can have one system, but you can link one system to multiple accounts. To inspect a system, you can pull up its *system card*. Below is an example system with all options set, which you can also see by typing `pk;system exmpl` on Discord:
![Example of a filled out system card]({% link /assets/ExampleSystem.png %})

### Parts of the system
These are the parts of the system, reading the card top to bottom left to right like a book:
1. **System name**: The system name is at the top of the system card. On the example system it is **PluralKit Example System**. System names are optional.
2. **Avatar**: This is the system avatar, it is an image that you can put on the system card.
3. **Fronters**: This shows who is currently registered as fronting using the [switch tracking commands](/guide#managing-switches). [It can be hidden.](/guide#system-privacy)
4. **Tag**: This is the system tag, and it will be placed after the name of members on proxied messages.
5. **Members**: This is a member count, and you can use `pk;system list` to see a list of the members.
6. **Description**: This is the system description, and can be any text you wish to put on your system card.
7. **System ID**: This is the auto-generated ID of the system. You can use this to refer to any system.
8. **Created on**: This is the time and date the system was created on.

### Creating a system
You need a system to start creating members, but luckily this is simple! All you need to do is run `pk;system new [name]`. So if you wanted your system to be named "Boxes of Foxes" you would run the command `pk;system new Boxes of Foxes`! If you don't want a name, you can use the command `pk;system new`, with no extra text.
If you want to do tweak the system, [see the user guide](/guide#system-management)! This page is just a simple setup guide.

----

## Members
Once you have created a system, the next thing you need to get started is to create a member! Like before, here is an example of a full member card with all options used:
![Example of a filled out member card]({% link /assets/ExampleMember.png %})

### Parts to a member
These are the parts of a member, reading the card top to bottom left to right like a book:
1. **Name and system**: This is the name of the member, in this case "Myriad", and the system they are part of in brackets, in this case the "PluralKit Example System".
2. **Avatar**: This is the image that's used for proxying messages from this member.
3. **Display name**: This is shown in proxy messages on all servers in place of their name if it is set.
4. **Server nickname**: This is like display name, but set for specific servers (in this case the PluralKit support server).
5. **Birthdate**: This is the member's set birthday, it can be with or without the year.
6. **Pronouns**: This is the member's pronouns. It's a free text entry field, so you can put anything you'd like.
7. **Message count**: This is the total number of messages this member has ever sent through proxies.
8. **Proxy tags**: This is a list of the member's proxy tags. We will go more in depth into them in a bit!
9. **Colour**: This is the member's colour, it affects the sidebar of the card on the left. **It does not affect the proxy colour, due to a Discord limitation.**
10. **Description**: This is the member description. Similar to the system description, you can put anything you'd like here.
11. **System ID**: This is the ID of the system this member is part of. In this case it's the ID for the PluralKit Example System we saw above.
12. **Member ID**: This is the auto-generated ID for the member. You can always use this ID to refer to a specific member, even if the name contains hard-to-type characters.
13. **Created on**: This is the date and time the member was created.

### Creating a member
This is just as easy as creating a system, but there are a few more things you will want to do immediately after. First you run `pk;member new <name>`, so if you want to create a member named Myriad, you would run `pk;member new Myriad`.
Next, for proxying later, you will want to set an avatar for your new member! This is done simply by using `pk;member <member> avatar <link to avatar>`. For example, 
```
pk;member Myriad avatar http://pluralkit.me/assets/myriad.png
```

You can also leave out the image link, and instead attach an image with the command. That'll work too!

For more info on what you can do with members, check out [the member management section of the user guide](/guide#member-management).

----

## Proxies
Proxies are probably the most important part of PluralKit, they are literally what the bot was made for. Below is an example of a proxied message:

![Example of a proxy message]({% link /assets/ExampleProxy.png %})

### Parts to a proxy message
1. **The name**: This is the member's name, display name, or server nickname, depending on what's set (server nickname overrides display name, which overrides the normal name). In this case, it's **Myriad "Big Boss" Kit**.
2. **The system tag**: If a system tag is set, this will will appear right after the name. In this case, it's **\| PluralKit 🦊**.
3. **The BOT badge**: All proxies have this due to how the proxy service works. It's not possible to remove.
3. **The message**: The message you proxied through the bot - this is what was on the "inside" of the proxy tags, which we will explain below.

### Parts to a proxy tag
A proxy tag is what tells PluralKit how to know to proxy a member. It looks like this:
```
pretextpost
```
and has 3 parts.
1. **Prefix**: In this case, `pre`. This tells PluralKit what to look for at the **start** of the message
2. **Separator**: This is always the word `text`, all-lowercase. It tells PluralKit where the prefix ends and the suffix starts.
3. **Suffix**: In this case, `post`. This tells PluralKit to what to look for at the **end** of a message 

You can imagine that this is an "example proxy", where the intended message is the word `text`.

In this example, typing a message such as
```
pre This is an example message post
```
would result in the message "This is an example message" being proxied.

### Setting a proxy tag
To set a proxy tag you need to know 3 things. The member you wish to set the tags for, the prefix for the tag, and the suffix for the tag.
Once you know these things you can run the command:
```
pk;member [member] proxy [prefix]text[suffix]
```
For example, if you want messages starting with `{` and ending with `}` to be proxied by Myriad, you could run
```
pk;member Myriad proxy {text}
```
Now when you type a message such as `{this is an example message}` it will be proxied by Myriad.

You do not need both to set it. If you do not set a prefix or a suffix, it will not care what is at the start or end of the message respectively. For more examples [click here](#more-proxy-examples)

For a more detailed guide on proxying, have a look at the [proxying section of the user guide](/guide#proxying).

### Reactions
When you come across a proxied message, or you have proxied a message, there are a few handy reactions you can add to the message for some more functionality!

❌ (red X): This reaction will cause the message to be deleted, but only if you are using the account that sent the message.

❓ (question mark): This reaction will DM you a message containing details on who sent the message, the member that it proxied, and the system it was from. When you react with this, you will receive a DM that looks like this:
![Example of a message query]({% link /assets/ExampleQuery.png %})

❗ (exclamation mark): This reaction will send a message to the channel the proxied message was sent in, pinging both you and the sender of the message. That message will look like this:
![Example of a message query]({% link /assets/ExamplePing.png %})

### More proxy examples
How to read these examples: The smaller code block with "Example Message" in it is the message you would like to proxy, the larger code block immediately after it is the command you would need to set the member Myriad to respond to that proxy

`Example message - Myriad`
```
pk;member Myriad proxy text- Myriad
```

`M. Example message`
```
pk;member Myriad proxy M.text
```

`🎩Example message`
```
pk;member Myriad proxy 🎩text
```
*Note: Custom emojis do work, but look a bit weird in the text form*

`-- Example message --`
```
pk;member Myriad proxy -- text --
```
*Note: Having a space between the prefix/suffix and `text` will mean that the space is required. In this example `--Example message--` will not proxy.*