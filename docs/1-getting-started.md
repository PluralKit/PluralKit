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

## The System
The first thing you need to do to use PluralKit is to set up a system! Here is an example system, you can see this by running `pk;system exmpl` on discord!
![Example of a filled out system card]({% link /assets/ExampleSystem.png %})
### Parts to the system
These are the parts of the system, reading the card top to bottom left to right like a book
1. System Name: The system name is at the top of the system card. On the example system it is **PluralKit Example System**
2. Avatar: This is the system avatar, it is an image that you can put on the system card
3. Fronters: This is who is currently fronting in the system. [It is toggleable](% link /guide#system-privacy%)
4. Tag: This is the system tag, it will be placed after the name of members on proxied messages
5. Members: This is a member count, you can use `pk;system list` to see a list of them
6. Description: This is the system description, it is just whatever you wish to put on your system card
7. System ID: This is the ID of the system
8. Created On: This is the time and date the system was created on

### Creating a system
You need a system to start creating members, but luckily this is relatively simple! All you need to do is run `pk;system new [name]`. So if you wanted your system to be named "Boxes of Foxes" you would run the command `pk;system new Boxes of Foxes`! Super simple!
If you want to do more with the system, [click here](% link /guide#system-management %)! This page is just a simple setup guide

----

## Members
Once you have created a system, the next thing you need to get started is to create a member! Here is an example of a full member card:
![Example of a filled out member card]({% link /assets/ExampleMember.png%})
### Parts to a member
These are the parts of a member, reading the card top to bottom left to right like a book
1. Name and system: This is the name of the member, in this case Myriad, and the system they are part of in brackets, in this case the PluralKit example system
2. Avatar: This is what image is used for proxy messages from this member
3. Display name: This is shown in proxy messages on all servers in place of their name if it is set
4. Server nickname: This is like display name, but it is only for the server it is set on
5. Birthdate: This is just simply the members birthday, it can be with or without the year
6. Pronouns: This is the members pronouns
7. Message count: This is the total number of messages this member has ever sent through proxies
8. Proxy tags: This is a list of the members proxy tags. we will go more in depth into them in a bit
9. Colour: This is the member's colour, it effects the sidebar of the card on the left. **It does not effect the proxy colour**
10. Description: This is the member description, same as the system description you can put anything you'd like here
11. System ID: This is the ID of the system this member is part of, in this case it is the ID for the PluralKit Example System
12. Member ID: This is the ID for the member
13. Creation on: This is the date and time the member was created

### Creating a member
This is just as easy as creating a system, but there are a few more things you will want to do immediately after! First you run `pk;member new [name]`, so if you want to create a member named Myriad, you would run `pk;member new Myriad`.
Next, for proxying later, you will want to set an avatar for your new member! This is done simply by using `pk;member [member] avatar [link to avatar]`. For example, 
```
pk;member Myriad avatar {{ site.url }}{% link /assets/myriad.png%}
```

For more info on what you can do with members check out [the member management section](/guide#member-management)

----

## Proxies
Proxies are probably the most important part of PluralKit, they are literally what the bot was made for.
![Example of a proxy message]({% link /assets/ExampleProxy.png %}w)

### Parts to a proxy message
1. The Name: This is the members name, display name, or server nickname, depending on the highest priority (Server nickname is higher then display name which is higher then name)
2. The tag: If a system tag is set, that is what will appear right after the name
3. The Bot Badge: All proxies have this due to how the proxy service works
3. The message: What you intended to send

### Parts to a proxy tag
A proxy tag is what tells PluralKit how to know to proxy a member. It looks like this:
```
pretextpost
```
and has 3 parts.
1. Prefix: In this case, `pre`. This tells PluralKit what to look for at the **start** of the message
2. Separator: This is always the word `text`. It tells PluralKit where the prefix ends and the suffix
3. Suffix: In this case, `post`. This tells PluralKit to what to look for at the **end** of a message 

In this example, typing a message such as
```
pre This is an example message post
```
would result in the message being proxied

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
Now when you type a message such as `{this is an example message}` it will be proxied by Myriad

You do not need both to set it. If you do not set a prefix or a suffix, it will not care what is at the start or end of the message respectively. For more examples [click here](#more-examples)

For a more detailed guide on proxying have a look at the [proxying section](/guide#proxying)

### Reactions
When you come across a proxied message, or you have proxied a message, there are a few handy reactions you can add to the message for some more functionality!

❌: This reaction will cause the message to be deleted but only if you are using the account that sent the message

❓: This reaction will DM you a message containing details on who sent the message, the member that it proxied, and the system it was from. When you react with this you will receive a DM that looks like this:
![Example of a message query]({% link /assets/ExampleQuery.png %})

❗: This reaction will send a message to the chat the proxied message was sent in, pinging both you and the sender of the message. That message will look like this:
![Example of a message query]({% link /assets/ExamplePing.png %})

### More examples
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
*Note: Custom emojis do work but look a bit weird in the text form*

`-- Example message --`
```
pk;member Myriad proxy -- text --
```
*Note: Having a space between the prefix/suffix and `text` will mean that the space is required. In this example `--Example message--` will not proxy*