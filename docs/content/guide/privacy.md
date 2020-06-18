# Privacy
By default, all information you save in PluralKit will be **public**. 
Using the lookup commands, anyone can look at your system and the information within.
This is often useful! The original intention of the bot was to function as a "profile", similar to someone's public Twitter or Tumblr account.

However, this may not be what everyone wants. 
As such, PluralKit offers options to make information **private** to others.
Every privacy option can be set to either **public** or **private**. Public data can be seen by anyone (including through [the API](../api.md)), wheras private data can only be seen by you.

::: warning
The privacy options only apply to lookups **by** others. 
When you look up information about your own system, **even in public servers**, all information will be shown.
So, be careful not to pull up private information in public :slightly_smiling_face:
:::

## System privacy
Systems currently have four privacy settings:

- **Description** privacy
- **Member list** privacy *(affects [member listing](./listing.md))*
- **Current front** privacy
- **Front/switch history** privacy

::: tip
It's possible to have a **private current front** and a **public front history**.
However, you can then see your current fronter through your front history.
This doesn't make much sense. :sweat_smile:
:::

You can view your system's current privacy settings using the following command:
<Cmd>system privacy</Cmd>

You can **change** your system's privacy settings using the following command:
<Cmd>system privacy <Arg>subject</Arg> <Arg>level</Arg></Cmd>

The argument <Arg>subject</Arg> should be either:
- <Arg>description</Arg>
- <Arg>list</Arg>
- <Arg>front</Arg>
- <Arg>fronthistory</Arg>
- <Arg>all</Arg> (changes <strong>every setting</strong> at once)
 
The argument <Arg>level</Arg> should be either <Arg>public</Arg> or <Arg>private</Arg>.

**For example:**
<CmdGroup>
<Cmd comment="Make your member list private">system privacy <Arg>list</Arg> <Arg>private</Arg></Cmd>
<Cmd comment="Make everything public">system privacy <Arg>all</Arg> <Arg>public</Arg></Cmd>
</CmdGroup>

## Member privacy
Members currently have six privacy settings:

- **Name** privacy
- **Description** privacy
- **Birthday** privacy
- **Pronoun** privacy
- **Metadata** privacy *(affects message count, last message, last switch, etc)*
- **Visibility** *(affects whether this member is shown in your system's [member lists](./listing.md))*

::: warning
There are a couple gotchas with these settings:
- Since PluralKit still needs to refer to a member, **name privacy only applies when a display name is set**. Any time PluralKit needs to show the member name, it'll *instead* show the display name.
- All members can always be looked up by [5-character ID](./ids.md), **even when visibility is private**. However, these IDs are impractical to guess, so as long as you don't share it, it'll still be private.
:::

You can view your member's current privacy settings using the following command:
<Cmd>member <Arg>member</Arg> privacy</Cmd>

You can **change** your system's privacy settings using the following command:
<Cmd>member <Arg>member</Arg> privacy <Arg>subject</Arg> <Arg>level</Arg></Cmd>

The argument <Arg>subject</Arg> should be either:
- <Arg>name</Arg>
- <Arg>description</Arg>
- <Arg>birthday</Arg>
- <Arg>pronouns</Arg>
- <Arg>metadata</Arg>
- <Arg>visibility</Arg>
- <Arg>all</Arg> (changes <strong>every setting</strong> at once)
 
The argument <Arg>level</Arg> should be either <Arg>public</Arg> or <Arg>private</Arg>.

**For example:**
<CmdGroup>
<Cmd comment="Make Craig's description private">member <Arg>Craig</Arg> privacy <Arg>description</Arg> <Arg>private</Arg></Cmd>
<Cmd comment="Hide Jane from member lists">member <Arg>Jane</Arg> privacy <Arg>visibility</Arg> <Arg>private</Arg></Cmd>
</CmdGroup>
