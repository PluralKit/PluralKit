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